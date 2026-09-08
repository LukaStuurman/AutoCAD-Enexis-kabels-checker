# AutoCAD Enexis kabels checker

AutoCAD .NET-plugin om per laagspanningsrichting de maximaal toegestane gG-afzekering en de bijbehorende maximale ontwerpstroom te bepalen.

De rekenregels zijn overgenomen uit `Eea-0205.K 1.0.xlsx`, specifiek uit:

- `Controle_kabel_evenredig` — evenredig verdeelde belasting over de kabel (50%)
- `Controle_kabel_laatste_helft` — belasting geconcentreerd op de laatste helft van de kabel (75%)

## Makkelijk installeren vanuit Releases

De GitHub Release bevat één ZIP voor **AutoCAD 2025, 2026, 2027 en nieuwere binary-compatible AutoCAD-versies (Windows 64-bit)**.

1. download de nieuwste `EnexisKabelChecker-AutoCAD-2025-plus-v*.zip` vanuit GitHub Releases;
2. pak de ZIP volledig uit;
3. sluit AutoCAD;
4. dubbelklik `INSTALLEREN.bat` en bevestig de Windows UAC-melding;
5. start AutoCAD opnieuw;
6. typ `ENEXISKABELCHECK`.

De installer kopieert `EnexisKabelChecker.bundle` naar `C:\Program Files\Autodesk\ApplicationPlugins`. Met `VERWIJDEREN.bat` kan dezelfde plugin weer worden verwijderd.

## AutoCAD-versies en runtimes

De release bevat twee afzonderlijke builds en AutoCAD kiest via `PackageContents.xml` automatisch de juiste variant:

- **AutoCAD 2025/2026 (R25.0-R25.1):** .NET 8, gebouwd tegen AutoCAD Managed .NET API 25.0.
- **AutoCAD 2027 (R26.0):** .NET 10, gebouwd tegen AutoCAD Managed .NET API 26.0.
- **Nieuwere AutoCAD-versies:** de R26/.NET 10-component heeft bewust geen `SeriesMax`, zodat een latere AutoCAD-versie hem kan laden zolang Autodesk binary compatibility met de R26 Managed .NET API behoudt.

AutoCAD 2027 is niet binary-compatible met 2025/2026; daarom is één .NET 8-DLL voor alle versies niet voldoende. Als Autodesk in een toekomstige release opnieuw een binary-incompatibele Managed .NET/API-generatie introduceert, moet daarvoor een extra buildtarget worden toegevoegd. De project-, bundle- en CI-structuur zijn nu per API-generatie opgezet zodat zo'n uitbreiding beperkt blijft.

## Werkwijze in AutoCAD

Start `ENEXISKABELCHECK` en bouw één richting stap voor stap op:

1. kies het kabeltype, bijvoorbeeld `150Al`;
2. klik op **Polyline kiezen + toevoegen**;
3. selecteer de bijbehorende polyline in de tekening;
4. de plugin leest automatisch de volledige polyline-lengte en voegt die als segment aan de richting toe;
5. kies eventueel een ander kabeltype, bijvoorbeeld `95Al`, en selecteer de volgende polyline van dezelfde richting;
6. herhaal dit totdat de hele richting is opgebouwd;
7. kies de juiste belastingsituatie en klik op **Bereken richting**;
8. bekijk de maximaal toegestane gG-afzekering en maximale ontwerpstroom;
9. klik op **Reset richting** om direct met een volgende richting te beginnen.

Ieder gekozen kabeldeel blijft als afzonderlijk segment zichtbaar. Een verkeerd gekozen segment kan met **Geselecteerd segment verwijderen** weer uit de richting worden gehaald. Als hetzelfde kabeltype meerdere keren voorkomt, worden de lengtes voor de elektrische berekening automatisch samengevoegd.

De polyline-lengte wordt omgerekend naar meters op basis van `INSUNITS`. Bij een niet-herkende tekeneenheid behandelt de plugin één tekeneenheid als één meter en toont hij een waarschuwing.

## Resultaat

De checker beoordeelt de reeks 63 / 80 / 100 / 125 / 160 / 200 / 250 A gG en geeft de hoogste toegestane combinatie terug. De bijbehorende maximale ontwerpstromen zijn 57 / 72 / 90 / 113 / 144 / 180 / 225 A.

De controle bestaat uit twee delen:

1. totale samengestelde kabelimpedantie: `Z = sqrt(Rtotaal² + Xtotaal²)`;
2. stroombelastbaarheid van iedere gebruikte kabeldoorsnede.

Een zekering is alleen toegestaan wanneer beide controles voldoen.

## AutoCAD-commando's

- `ENEXISKABELCHECK` — aanbevolen workflow: kabeltype kiezen, polyline aanklikken en zo één volledige richting opbouwen.
- `ENEXISKABELCHECKSEL` — alternatieve bulkselectie: selecteert meerdere kabelcurves tegelijk en probeert het kabeltype uit de laagnaam te herkennen. De gevonden delen worden daarna in dezelfde richting-builder geladen.

## Ondersteunde kabeltypen

Aluminium: 4x240, 4x150, 4x120, 4x95, 4x70, 4x50, 4x35, 4x25 en 4x16 mm².

Koper: 4x185, 4x150, 4x95, 4x70, 4x50, 4x35, 4x25 en 4x16 mm².

## Bouwen voor ontwikkeling

De standaardbuild gebruikt de AutoCAD.NET NuGet-referenties en bouwt zowel R25/.NET 8 als R26/.NET 10. Zorg dat de .NET 8- en .NET 10-SDK beschikbaar zijn:

```powershell
.\build.ps1
```

Wie liever tegen lokaal geïnstalleerde Autodesk-DLL's bouwt kan per generatie een map meegeven:

```powershell
.\build.ps1 `
  -AutoCADManagedDir "C:\Program Files\Autodesk\AutoCAD 2026" `
  -AutoCAD2027ManagedDir "C:\Program Files\Autodesk\AutoCAD 2027"
```

De gecombineerde bundel komt in `dist\EnexisKabelChecker.bundle`. Daarin staan de R25-build onder `Contents\Windows\net8` en de R26-build onder `Contents\Windows\net10`.

Voor ontwikkeling kan de passende `Enexis.KabelChecker.AutoCAD.dll` ook met `NETLOAD` worden geladen.

## CI-compatibiliteitscontrole

`.github/workflows/autocad-compatibility.yml` compileert bij relevante pull requests en pushes beide ondersteunde AutoCAD API-generaties. De workflow controleert daarnaast dat `PackageContents.xml` AutoCAD 2025/2026 uitsluitend naar de .NET 8-build routeert en AutoCAD 2027+ naar de .NET 10-build.

## Belangrijk

De Excel-waarschuwing over kabelverjonging blijft gelden: zwaardere kabels horen aan het begin van de kabelgroep te liggen en dunnere kabels aan het einde. Omdat de nieuwe workflow ieder kabeldeel in selectievolgorde bewaart, kan hier later ook een automatische volgordecontrole aan worden toegevoegd.
