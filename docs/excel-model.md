# Herleiding van de Excel-rekenregels

Bron: `Eea-0205.K 1.0.xlsx`.

## Relevante tabbladen

- `Controle_kabel_evenredig`: belasting evenredig verdeeld over de kabel (50%).
- `Controle_kabel_laatste_helft`: belasting geconcentreerd op de laatste helft van de kabel (75%).

## gG-reeks en ontwerpstroom

| gG | maximale ontwerpstroom |
|---:|---:|
| 63 A | 57 A |
| 80 A | 72 A |
| 100 A | 90 A |
| 125 A | 113 A |
| 160 A | 144 A |
| 200 A | 180 A |
| 250 A | 225 A |

De maximale ontwerpstroom is de bovengrens uit de grijze rij van het werkblad, bijvoorbeeld `58 t/m 72A` -> 72 A.

## Maximale samengestelde impedantie

### Evenredig (50%)

| gG | Z max [ohm] |
|---:|---:|
| 63 A | 0.250 |
| 80 A | 0.250 |
| 100 A | 0.204 |
| 125 A | 0.163 |
| 160 A | 0.128 |
| 200 A | 0.092 |
| 250 A | 0.062 |

### Laatste helft (75%)

| gG | Z max [ohm] |
|---:|---:|
| 63 A | 0.215 |
| 80 A | 0.170 |
| 100 A | 0.136 |
| 125 A | 0.109 |
| 160 A | 0.085 |
| 200 A | 0.068 |
| 250 A | 0.055 |

## Berekening

Per gebruikte kabeldoorsnede:

- `Rdeel = R_per_km * lengte_m / 1000`
- `Xdeel = X_per_km * lengte_m / 1000`

Daarna:

- `Rtotaal = som(Rdeel)`
- `Xtotaal = som(Xdeel)`
- `Ztotaal = sqrt(Rtotaal^2 + Xtotaal^2)`

Een gG-stap is toegestaan wanneer:

1. `Ztotaal <= Zmax` voor die gG-stap; en
2. de maximale ontwerpstroom van die stap niet hoger is dan de laagste zomer-stroombelastbaarheid van alle gebruikte kabeldoorsnedes.

De plugin kiest de hoogste gG-stap waarvoor beide voorwaarden waar zijn.

## Kabelgegevens uit de werkbladen

| Kabel | R [ohm/km] | X [ohm/km] | zomer [A] |
|---|---:|---:|---:|
| 4*240mm2 Al | 0.129 | 0.073 | 343.6020 |
| 4*150mm2 Al | 0.206 | 0.079 | 260.2125 |
| 4*120mm2 Al | 0.281 | 0.081 | 232.8426 |
| 4*95mm2 Al | 0.320 | 0.082 | 203.6016 |
| 4*70mm2 Al | 0.443 | 0.0835 | 169.1442 |
| 4*50mm2 Al | 0.641 | 0.085 | 137.3436 |
| 4*35mm2 Al | 0.868 | 0.101 | 115.0929 |
| 4*25mm2 Al | 1.200 | 0.094 | 95.9931 |
| 4*16mm2 Al | 1.910 | 0.096 | 74.3337 |
| 4*185mm2 Cu | 0.107 | 0.068 | 382.9842 |
| 4*150mm2 Cu | 0.125 | 0.068 | 339.0741 |
| 4*95mm2 Cu | 0.194 | 0.069 | 266.5143 |
| 4*70mm2 Cu | 0.268 | 0.072 | 222.7014 |
| 4*50mm2 Cu | 0.387 | 0.085 | 179.9739 |
| 4*35mm2 Cu | 0.524 | 0.100 | 152.0127 |
| 4*25mm2 Cu | 0.727 | 0.094 | 126.6111 |
| 4*16mm2 Cu | 1.150 | 0.097 | 97.8642 |

## Referentiegevallen uit het aangeleverde bestand

Evenredig: 209.09 m 4*150mm2 Al + 68.59 m 4*95mm2 Al -> `Z = 0.06868816869589478 ohm` -> maximaal **200 A gG / 180 A ontwerpstroom**.

Laatste helft: 204 m 4*150mm2 Al + 127 m 4*120mm2 Al -> `Z = 0.08207385655615314 ohm` -> maximaal **160 A gG / 144 A ontwerpstroom**.

## Ontwerpwaarschuwing

Beide Excelbladen vermelden dat sprake moet zijn van kabelverjonging: de zwaardere kabels aan het begin van de kabelgroep en de dunnere kabels aan het einde. Het Excelblad zelf rekent met geaggregeerde lengtes per doorsnede en valideert de fysieke volgorde niet. De core-engine doet hetzelfde; de tekenvolgorde kan later als aanvullende AutoCAD-validatie worden toegevoegd wanneer vaststaat hoe een richting en de volgorde daarin in de Enexis-tekening zijn gemodelleerd.
