# Ontwerpstroom, verbruik en opwek

De bron voor de aansluitwaarden en faseverdeling is het ingebouwde Excelbestand van het geselecteerde kader. Kabel- en trafowaarden verschillen en worden apart bijgehouden. Voor elke kolom telt de plugin `aantal × waarde per eenheid` op. Automatisch kiest daarna het hoogste van de twee totalen, onafhankelijk voor kabelrichting en station. `Som(maximum per aansluiting)` is daarvoor niet geschikt: dat mengt twee verschillende situaties.

| Kader | Kabelblad: waarden / totalen | Trafoblad: waarden / totalen | Richtingen naar trafo |
| --- | --- | --- | --- |
| 2024 — 1.0 | Ontwerpstroom_kabel: C/D; E/F; C38 | Ontwerpstroom_trafo: C/D; E/F; C38/C39 | Plugin telt aantallen uit alle opgeslagen richtingen op in kolom A |
| 2025 — 2.0 | Ontwerpstroom_kabel: C/D; E/F; C45 | Ontwerpstroom_trafo: C/D; E/F; C45/C46 | Plugin telt aantallen uit alle opgeslagen richtingen op in kolom A |
| 2026 — 3.0 | (1) t/m (12): F/G; H/I; D77 | Transformator: F/G; H/I; F77/F78 | Kolom B verwijst naar overeenkomstige rij in twaalf richtingen |
| 2026 — 3.2 | (1) t/m (12): F/G; H/I; D81 | Transformator: F/G; H/I; F79/F80 | Vanaf kabelrij 45 ligt de traforij twee rijen eerder |

De oudere Excelbestanden bevatten losse kabel- en trafobladen. De plugin maakt kabelbladen per richting en vult het losse trafoblad met de stationaantallen. Ook bij deze kaders hoeft de gebruiker het trafoblad dus niet opnieuw in te vullen na een plugin-export. De 2026-bestanden hebben zelf twaalf richtingbladen met formulekoppelingen naar de trafo.

## Bijzonderheden

- 3.2 telt eenfasig straatmeubilair 1x6A en 1x10A in de kabel- en trafototalen mee met een factor 1/3, overeenkomstig H75/H76 op de richting en H73/H74 op de trafo. De herkenbare invoerwaarden blijven 4,3 A en 8,7 A.
- Een lege waarde voor opwek betekent nul in die kolom. Het type en aantal blijven bewaard bij wisselen naar Verbruik of Automatisch.
- De oorspronkelijke 3.2-template bevat een lege kabeltotaalcel D81, enkele vastgelegde nulwaarden in trafoaantallen en twee vermogenformules met een verwijzing naar een externe kopie. De export schrijft de gekozen kabeltotaalformule, herstelt de lokale trafoaantallen en verwijst die twee vermogensommen naar de eigen richtingbladen. De ingebouwde bronbestanden blijven behouden.
- 3.0 heeft geen vooringevulde aansluiting in (1)!B46. De eerder verwijderde voorbeeldinvoer blijft verwijderd.
- Tekststroom alleen identificeert niet altijd een uniek aansluittype. Een typekoppeling is vereist voordat beide kolommen en het station betrouwbaar kunnen worden berekend. Uit kader geeft een directe keuze; bij herkende maar dubbelzinnige tekst vraagt de plugin om aantallen per type.
- Wisselen tussen 3.0 en 3.2 hergebruikt equivalente types. Een kader zonder een gebruikt type vereist een nieuw station met lege richtingen, zodat aansluitingen niet stilzwijgend verdwijnen.
- Het stationoverzicht omvat opgeslagen richtingen plus de huidige invoer als voorvertoning. Bij bewerken vervangt die invoer de opgeslagen versie van dezelfde richting. Sla richtingen op voordat je exporteert; de export bevat de opgeslagen richtingen.

## Verificatie

VerifyKaderTemplates controleert alle 136 aansluitopties tegen beide bronkolommen en de trafowaarden, inclusief faseverdeling. Exports met alle types in richting 1 en 12 worden voor elk kader en elke stroombasis nagerekend. Ook nul-opwek, gescheiden maxima, behoud van typekoppelingen, opslag van de keuze en migratie van oude stations worden gecontroleerd. De bestaande controles voor twaalf richtingbladen, kabelprofielen, lengtes en overige templateformules blijven actief. build.ps1 voert deze controles uit op .NET 8 en .NET 10.
