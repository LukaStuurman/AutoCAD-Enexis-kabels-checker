# AutoCAD Enexis kabels checker

AutoCAD .NET-plugin om per laagspanningsrichting de maximaal toegestane gG-afzekering en de bijbehorende maximale ontwerpstroom te bepalen.

De rekenregels zijn overgenomen uit `Eea-0205.K 1.0.xlsx`, specifiek uit:

- `Controle_kabel_evenredig` — evenredig verdeelde belasting over de kabel (50%)
- `Controle_kabel_laatste_helft` — belasting geconcentreerd op de laatste helft van de kabel (75%)

## Resultaat

De checker beoordeelt de reeks 63 / 80 / 100 / 125 / 160 / 200 / 250 A gG en geeft de hoogste toegestane combinatie terug. De bijbehorende maximale ontwerpstromen zijn 57 / 72 / 90 / 113 / 144 / 180 / 225 A.

De controle bestaat uit twee delen:

1. totale samengestelde kabelimpedantie: `Z = sqrt(Rtotaal² + Xtotaal²)`;
2. stroombelastbaarheid van iedere gebruikte kabeldoorsnede.

Een zekering is alleen toegestaan wanneer beide controles voldoen.

## AutoCAD-commando's

- `ENEXISKABELCHECK` — opent de calculator en laat kabellengtes handmatig invoeren.
- `ENEXISKABELCHECKSEL` — laat kabelobjecten in de tekening selecteren, telt de lengtes op en probeert het kabeltype uit de laagnaam te herkennen. Daarna opent dezelfde calculator met de gevonden lengtes ingevuld.

## Ondersteunde kabeltypen

Aluminium: 4x240, 4x150, 4x120, 4x95, 4x70, 4x50, 4x35, 4x25 en 4x16 mm².

Koper: 4x185, 4x150, 4x95, 4x70, 4x50, 4x35, 4x25 en 4x16 mm².

## AutoCAD-versie

De eerste build richt zich op AutoCAD 2025/2026 en .NET 8. De AutoCAD-projectfile verwijst standaard naar de managed DLL's van AutoCAD 2025; het pad kan tijdens de build worden overschreven met `AutoCADManagedDir`.

## Bouwen

```powershell
.\build.ps1
```

Of met een afwijkende AutoCAD-installatiemap:

```powershell
.\build.ps1 -AutoCADManagedDir "C:\Program Files\Autodesk\AutoCAD 2026"
```

De bundel komt in `dist\EnexisKabelChecker.bundle`.

Voor ontwikkeling kan `Enexis.KabelChecker.AutoCAD.dll` ook met `NETLOAD` worden geladen.

## Belangrijk

De Excel-waarschuwing over kabelverjonging blijft gelden: zwaardere kabels horen aan het begin van de kabelgroep te liggen en dunnere kabels aan het einde. De huidige rekenengine controleert de elektrische waarden exact op basis van kabeltypen en lengtes, maar kan de fysieke volgorde van kabelverjonging niet betrouwbaar afleiden uit alleen geaggregeerde lengtes.
