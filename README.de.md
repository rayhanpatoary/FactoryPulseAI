# FactoryPulse AI

[English project overview](README.md)

FactoryPulse AI ist ein Portfolio-Projekt für die simulierte Überwachung von Industriemaschinen, Qualitätsprüfung und vorausschauende Wartung. In Stufe 2 von Tag 1 speichert die .NET-10-Anwendung Maschinen, Messwerte und Alarme mit EF Core und SQLite. Das Projekt arbeitet ohne Verbindung zu realen Industrieanlagen.

## Lokal starten

Vorausgesetzt werden Visual Studio Community 2026 mit dem Workload **ASP.NET und Webentwicklung** sowie ein stabiles .NET-10-SDK ab Version 10.0.404 innerhalb der Reihe 10.0.4xx. Alternativ kann das SDK gemäß [`global.json`](global.json) lokal im Projektordner `.dotnet/` liegen.

1. `FactoryPulseAI.slnx` in Visual Studio öffnen.
2. `FactoryPulse.Api` als Startprojekt festlegen und das Profil **FactoryPulse.Api (HTTPS)** wählen.
3. **F5** drücken. Bei einer Rückfrage das lokale ASP.NET-Core-Entwicklungszertifikat prüfen und für HTTPS bestätigen.
4. Über **Test > Alle Tests ausführen** die Unit- und Integrationstests starten.

| Endpunkt | Funktion |
| --- | --- |
| `https://localhost:7161/health` | Verfügbarkeit des API-Prozesses |
| `https://localhost:7161/api/status` | Konfigurierter Instanzname, Simulationsmodus und UTC-Zeitstempel |
| `https://localhost:7161/openapi/v1.json` | OpenAPI-Dokument in der Entwicklungsumgebung |

Die [englische Projektübersicht](README.md) enthält die Befehle für die Überprüfung auf der Kommandozeile. Die Migration für die lokale SQLite-Datenbank wird bei Bedarf explizit ausgeführt. Die englischsprachige Dokumentation erläutert [Datenbankeinrichtung und Migrationen](docs/architecture/DATABASE.md), [Anforderungen](docs/REQUIREMENTS.md) und [Architektur](docs/architecture/ARCHITECTURE.md).
