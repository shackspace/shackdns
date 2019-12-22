# Anforderungen

- Alle Anzeigen aktualisieren sich selbstständig und differenziell (kein kompletter Wiederaufbau/Refresh der Seite)

## Anzeige: Dynamische Leases
- Zeigt eine Liste aller aktuell verteilten dynamischen Leases an
- Zeigt für jeden Eintrag:
  - Die MAC-Adresse (mit Link auf die URL, siehe unten)
  - Datum "Erstes Erhalten des Leases"
  - Datum "Letzter Refresh des Leases"
  - Erreichbarkeit (1 Ping pro Sekunde)

## Anzeige: Statische Leases + DNS
- Zeigt eine Liste aller statisch verteilten Leases an
- Zeigt für jeden Eintrag IP(s) + DNS(s)
- Zeigt für jeden Eintrag:
  - Last Seen
  - Verfügbarkeit (Prozentsatz erfolgreicher Pings in der letzten Woche)
  - Für jede IP des Eintrag:
    - Erreichbarkeit (1 Ping pro Sekunde)

## Anzeige: Netzwerk-Struktur
- Welche Subnetze sind aus welchen VLans erreichbar?

## Anlegen: Statischer Lease
- Von wem angelegt?
- Menschenlesbarer Name / Titel
- Kurze Service-Beschreibung (Einzeiler)
- Welche MACs hat das Gerät?
- Ansprechpartner / Kontaktdaten
  - Mail o.ä.
- Projekt/Tags
- IP wird automatisch vergeben (Subnetze/VLans können pro MAC gewählt werden)
- Doku-Link (Wiki o.ä.)
- Wie lange soll der Eintrag gültig sein?
  - 1 Woche
  - 1 Monat
  - 1 Jahr
  - Endlos (benötigt Doku-Link)
- Nach Anlegen wird ein Lease-Token ausgegeben ($eindeutigerZufallswert)

## Bearbeiten: Statischer Lease
- Zur Änderung wird Token benötigt
- Lease-Dauer kann refreshed werden oder auf Endlos gestellt werden
- Alle Angaben außer "Ersteller" können geändert werden (insbesondere auch die MACs, relevant für Systemupdates!)

## Automatismus: Zerfall von veralteten Leases
- Wenn Lease zu 90% abgelaufen, wird Mail an Ansprechpartner geschickt
- Wenn Lease zu 100% abgelaufen ist, wird Lease deaktiviert
- Wenn Lease zu 200% abgelaufen ist, wird er gelöscht

## Automatismus: Automatischer DNS-Eintrag für alle vergebenen Leases
- Für jeden Lease (statisch oder dynamisch) wird ein Eintrag in der Form `XX-XX-XX-XX-XX-XX.mac.shack` vergeben
- Der Eintrag zeigt auf die IP-Adresse der MAC-Adresse
- Spart für kleine Projekte das Anlegen eines echten Eintrags, da im Projekt das Gerät mit der MAC-Adresse adressiert werden kann