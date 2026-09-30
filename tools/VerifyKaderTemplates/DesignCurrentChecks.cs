using System.Text.Json;
using ClosedXML.Excel;
using Enexis.KabelChecker.AutoCAD;
using Enexis.KabelChecker.Core;
using Enexis.KabelChecker.ExcelWorker;

internal static class DesignCurrentChecks
{
    public static void Run(string resources)
    {
        foreach (var version in Enum.GetValues<KaderVersion>())
        {
            KaderVersionSelection.SetCurrent(version);
            Check(KaderVersionSelection.CurrentMode == DesignCurrentMode.Automatisch, "Nieuw kader moet automatisch starten.");
            var definition = KaderVersions.Get(version);
            var options = ExcelLoadCatalog.For(version);
            var legacy = version is KaderVersion.K2024_1_0 or KaderVersion.K2025_2_0;
            var v30 = version == KaderVersion.K2026_3_0;
            var bytes = File.ReadAllBytes(Path.Combine(resources, definition.ResourceFileName));
            using var source = new XLWorkbook(new MemoryStream(bytes));
            var cable = source.Worksheet(legacy ? "Ontwerpstroom_kabel" : "(1)");
            var station = source.Worksheet(legacy ? "Ontwerpstroom_trafo" : "Transformator");
            var unitColumn = legacy ? 3 : 6;
            foreach (var option in options)
            {
                Near(option.CableConsumptionAmps, Value(cable.Cell(option.Row, unitColumn)), "Verbruik " + option.Key);
                Near(option.CableGenerationAmps, Value(cable.Cell(option.Row, unitColumn + 1)), "Opwek " + option.Key);
                var factor = cable.Cell(option.Row, unitColumn + 2).FormulaA1.EndsWith("/3") ? 1.0 / 3 : 1;
                Near(option.CablePhaseFactor, factor, "Kabel fasen " + option.Key);
                var stationFactor = station.Cell(option.StationRow, unitColumn + 2).FormulaA1.EndsWith("/3") ? 1.0 / 3 : 1;
                Near(option.StationConsumptionAmps, Value(station.Cell(option.StationRow, unitColumn)) * stationFactor, "Trafo verbruik " + option.Key);
                Near(option.StationGenerationAmps, Value(station.Cell(option.StationRow, unitColumn + 1)) * stationFactor, "Trafo opwek " + option.Key);
                Check(ExcelLoadCatalog.FindByAmps(version, option.CableConsumptionAmps, DesignCurrentMode.Verbruik).Contains(option), "Verbruikherkenning ontbreekt.");
                Check(ExcelLoadCatalog.FindByAmps(version, option.CableGenerationAmps, DesignCurrentMode.Opwek).Contains(option), "Opwekherkenning ontbreekt.");
            }

            var mapped = options.Select((x, i) => new ExcelMappedLoad(x.Key, x.CableDesignCurrentAmps, i + 1)).ToArray();
            var totals = DesignCurrentCalculator.Calculate(version, mapped);
            var wrong = options.Select((x, i) => Math.Max(x.CableConsumptionAmps, x.CableGenerationAmps) * x.CablePhaseFactor * (i + 1)).Sum();
            Check(wrong > totals.CableCurrent(DesignCurrentMode.Automatisch) + 1e-6, "Regressiegeval moet maximum per aansluiting onderscheiden.");
            var consuming = options.First(x => x.StationConsumptionAmps > x.StationGenerationAmps);
            var generating = options.First(x => x.StationGenerationAmps > x.StationConsumptionAmps);
            var directionA = new[] { new ExcelMappedLoad(consuming.Key, consuming.CableConsumptionAmps, 2) };
            var directionB = new[] { new ExcelMappedLoad(generating.Key, generating.CableGenerationAmps, 2) };
            var stationTotal = DesignCurrentCalculator.Calculate(version, directionA.Concat(directionB)).StationCurrent(DesignCurrentMode.Automatisch);
            var wrongStationTotal = DesignCurrentCalculator.Calculate(version, directionA).StationCurrent(DesignCurrentMode.Automatisch)
                + DesignCurrentCalculator.Calculate(version, directionB).StationCurrent(DesignCurrentMode.Automatisch);
            Check(wrongStationTotal > stationTotal, "Station mag geen optelling van afzonderlijke richtingmaxima zijn.");

            foreach (var mode in Enum.GetValues<DesignCurrentMode>())
            {
                KaderVersionSelection.SetMode(mode);
                Near(totals.CableCurrent(mode), mode switch
                {
                    DesignCurrentMode.Verbruik => totals.CableConsumptionAmps,
                    DesignCurrentMode.Opwek => totals.CableGenerationAmps,
                    _ => Math.Max(totals.CableConsumptionAmps, totals.CableGenerationAmps)
                }, "Modus");
                var layout = version switch { KaderVersion.K2024_1_0 => "Legacy2024", KaderVersion.K2025_2_0 => "Legacy2025", KaderVersion.K2026_3_0 => "V30", _ => "V32" };
                var request = JsonSerializer.Serialize(new
                {
                    Layout = layout, CountFirstRow = options.Min(x => x.Row), CountLastRow = options.Max(x => x.Row), CurrentMode = mode.ToString(),
                    Directions = new[] { 1, 12 }.Select(n => new
                    {
                        Number = n, Evenredig = n == 1,
                        Loads = options.Select((x, i) => new { x.Row, x.StationRow, Count = i + 1 }).ToArray(),
                        Segments = Array.Empty<object>()
                    }).ToArray()
                });
                var path = Path.Combine(Path.GetTempPath(), "Enexis-current-" + Guid.NewGuid().ToString("N") + ".xlsx");
                try
                {
                    ExcelWorker.Export(path, bytes, request);
                    using var output = new XLWorkbook(path);
                    var trafo = output.Worksheet(legacy ? "Ontwerpstroom_trafo" : "Transformator");
                    foreach (var option in options)
                        Near(trafo.Cell(option.StationRow, legacy ? 1 : 2).GetDouble(), (Array.IndexOf(options.ToArray(), option) + 1) * 2, "Stationaantal " + option.Key);
                    foreach (var number in new[] { 1, 12 })
                    {
                        var sheet = output.Worksheet(legacy ? $"Ontwerpstroom_kabel R{number}" : $"({number})");
                        var cell = sheet.Cell(legacy ? version == KaderVersion.K2024_1_0 ? 38 : 45 : v30 ? 77 : 81, legacy ? 3 : 4);
                        Near(cell.GetDouble(), totals.CableCurrent(mode), $"{layout}/{mode} richting {number}");
                    }
                    var totalRow = version switch { KaderVersion.K2024_1_0 => 38, KaderVersion.K2025_2_0 => 45, KaderVersion.K2026_3_0 => 77, _ => 79 };
                    Near(trafo.Cell(totalRow, legacy ? 3 : 6).GetDouble(), totals.StationConsumptionAmps * 2, $"{layout} stationverbruik");
                    Near(trafo.Cell(totalRow + 1, legacy ? 3 : 6).GetDouble(), totals.StationGenerationAmps * 2, $"{layout} stationopwek");
                    foreach (var sheet in output.Worksheets)
                        Check(sheet.CellsUsed().Where(x => x.HasFormula).All(x => !x.FormulaA1.Contains("#REF!")), $"Verbroken bladverwijzing in {sheet.Name}");
                }
                finally { if (File.Exists(path)) File.Delete(path); }
            }
            Console.WriteLine($"OK - {definition.DisplayName}: beide kabel/trafo-kolommen, faseverdeling, drie modi en stationexport van richtingen 1 + 12.");
        }

        // A station total may choose a different column than one individual direction.
        var mixed = new DesignCurrentTotals(10, 9, 8, 12, 2);
        Near(mixed.CableCurrent(DesignCurrentMode.Automatisch), 10, "Kabelbasis onafhankelijk");
        Near(mixed.StationCurrent(DesignCurrentMode.Automatisch), 12, "Stationbasis onafhankelijk");
        Check(mixed.CableBasis(DesignCurrentMode.Automatisch) == "verbruik" && mixed.StationBasis(DesignCurrentMode.Automatisch) == "opwek", "Verkeerde stroombasis.");
        Check(DesignCurrentCalculator.MatchesInputs(new[] { new CurrentLoadInput(0, 2) }, new[] { new ExcelMappedLoad("zero", 0, 2) }), "Nul opwek is geldig voor gekozen aansluittype.");
        Check(!DesignCurrentCalculator.MatchesInputs(new[] { new CurrentLoadInput(8, 3) }, new[] { new ExcelMappedLoad("type", 8, 2) }), "Gewijzigd aantal mag niet stil hergebruikt worden.");
        Check(ExcelLoadCatalog.FindByKey(KaderVersion.K2026_3_0, "V32_T11_1X6A") is null, "Ontbrekend type mag niet overgezet worden.");
        Check(ExcelLoadCatalog.FindByKey(KaderVersion.K2026_3_0, "V32_T1_VRIJSTAAND") is not null, "Gelijk 3.0/3.2 type moet kunnen wisselen.");
        var known = ExcelLoadCatalog.For(KaderVersion.K2026_3_0).First();
        KaderVersionSelection.SetCurrent(KaderVersion.K2026_3_0);
        KaderVersionSelection.SetMode(DesignCurrentMode.Opwek);
        var remapped = ExcelLoadResolver.Resolve(null, new[] { new CurrentLoadInput(known.CableConsumptionAmps, 3) }, KaderVersion.K2026_3_0,
            new[] { new ExcelMappedLoad("V32_T1_VRIJSTAAND", known.CableConsumptionAmps, 2) });
        Check(remapped is { Count: 1 } && remapped[0].Count == 3 && remapped[0].ExcelLoadKey == known.Key,
            "Bekend type moet behouden blijven na wijzigen aantal, modus en 3.0/3.2 kader.");
        KaderVersionSelection.SetCurrent(KaderVersion.K2024_1_0);
        KaderVersionSelection.SetMode(DesignCurrentMode.Verbruik);
        KaderVersionSelection.SetCurrent(KaderVersion.K2026_3_0);
        KaderVersionSelection.SetMode(DesignCurrentMode.Opwek);
        KaderVersionSelection.SetCurrent(KaderVersion.K2024_1_0);
        Check(KaderVersionSelection.CurrentMode == DesignCurrentMode.Verbruik, "Modus moet per kader onthouden worden.");
        CheckPersistence();
        KaderVersionSelection.SetCurrent(KaderVersion.K2026_3_2);
        KaderVersionSelection.SetMode(DesignCurrentMode.Automatisch);
    }

    private static void CheckPersistence()
    {
        var path = Path.Combine(Path.GetTempPath(), "Enexis-stations-" + Guid.NewGuid().ToString("N"), "stations.json");
        var store = new StationPersistence(path);
        var option = ExcelLoadCatalog.For(KaderVersion.K2026_3_0).First();
        var direction = new DirectionState(1, LoadProfile.Evenredig, new[] { new CableSegment("test", 12) },
            new[] { new CurrentLoadInput(option.CableConsumptionAmps, 2) }, new[] { new ExcelMappedLoad(option.Key, option.CableConsumptionAmps, 2) });
        try
        {
            foreach (var mode in Enum.GetValues<DesignCurrentMode>())
            {
                store.Save("test", KaderVersion.K2026_3_0, new[] { direction }, mode);
                var loaded = new StationPersistence(path).Load("test")!;
                Check(loaded.CurrentMode == mode && loaded.KaderVersion == KaderVersion.K2026_3_0 && loaded.Directions[0].ExcelLoads[0] == direction.ExcelLoads[0], "Station roundtrip.");
            }
            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
            json["Stations"]![0]!.AsObject().Remove("CurrentMode");
            File.WriteAllText(path, json.ToJsonString());
            Check(store.Load("test")!.CurrentMode == DesignCurrentMode.Automatisch, "Oud station start automatisch.");
        }
        finally { if (File.Exists(path)) File.Delete(path); Directory.Delete(Path.GetDirectoryName(path)!); }
    }

    private static double Value(IXLCell cell) => cell.IsEmpty() ? 0 : cell.GetDouble();
    private static void Near(double actual, double expected, string message) => Check(Math.Abs(actual - expected) < 1e-7, $"{message}: {actual} != {expected}");
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
