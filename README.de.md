# FactoryPulse AI

[English](README.md)

**Plattform für industrielle Maschinenüberwachung, Diagnose und intelligente Qualitätsprüfung**

FactoryPulse AI soll Messwerte, Maschinenzustände und Alarme für die Zustandsüberwachung und Fehlerdiagnose zusammenführen. Die Plattform richtet sich an Teams aus industrieller Softwareentwicklung, Instandhaltung, Maschinenbedienung und Qualitätssicherung, die Betriebsverläufe und Störungen anhand einer gemeinsamen Datengrundlage beurteilen möchten. Zur Produktvision gehören kamerabasierte Prüfungen und die Analyse von Fertigungsdaten.

## Verfügbare Funktionen

- **Status-API.** ASP.NET Core stellt Endpunkte für die Erreichbarkeit des API-Prozesses und den Anwendungsstatus bereit. In der Entwicklungsumgebung steht ein OpenAPI-Dokument zur Verfügung.
- **Maschinendatenmodell.** Fachliche Regeln prüfen Maschinen, Messwerte mit Zeitstempel und Alarme. Bei der Alarmquittierung bleibt der Zeitpunkt der ersten Quittierung erhalten.
- **Simulierte Datenerfassung.** Ein zuschaltbarer Simulator erzeugt reproduzierbare Telemetrie für drei CNC-Profile und speichert die jeweils neuesten Messwerte gesammelt in SQLite.
- **API für die Maschinenüberwachung.** Maschinenbestand, Anzahl je Status, aktuelle Messwerte und den paginierten Messwertverlauf mit UTC-Zeitfiltern abrufen. [Endpunkte und Beispiele](docs/architecture/MONITORING-API.md)
- **Alarmverwaltung.** Konfigurierbare Regeln für Temperatur und Schwingung erkennen auffällige Messwerte und beenden Alarme nach Rückkehr in den Normalbereich. Die REST-API liefert aktive Alarme und den Verlauf. [Alarmeinstellungen und Endpunkte](docs/architecture/ALARMS.md)
- **Echtzeitüberwachung.** SignalR überträgt aktuelle Messwerte und gespeicherte Alarmänderungen an angemeldete Personen. In der Produktionsumgebung muss der Stream ausdrücklich aktiviert werden. [Hub und Ereignisformate](docs/architecture/REALTIME.md)
- **Web-Dashboard.** Die Angular-Oberfläche zeigt Maschinenzustände, Messwertdiagramme, Alarme und den paginierten Messwertverlauf auf Deutsch und Englisch. [Einrichtung und Voraussetzungen](docs/architecture/WEB.md)
- **Datenpersistenz mit SQLite.** EF Core speichert Maschinen, Messwerte und Alarme mit indizierten Beziehungen und expliziten Schemamigrationen.

## Softwarearchitektur und Technologien

Die Lösung ist ein modularer Monolith auf Basis von .NET 10. Vier Anwendungsprojekte trennen die Fachlogik von Datenpersistenz und HTTP-Schnittstellen.

| Projekt | Verantwortung |
| --- | --- |
| `FactoryPulse.Domain` | Modelle und Validierung für Maschinen, Messwerte und Alarme |
| `FactoryPulse.Application` | Anwendungsfälle und Schnittstellenverträge, einschließlich Anwendungsstatus |
| `FactoryPulse.Infrastructure` | SQLite-Mappings mit EF Core 10, Migrationen und Verlaufsabfragen |
| `FactoryPulse.Api` | ASP.NET Core, HTTP-Endpunkte, Konfiguration, Dependency Injection und OpenAPI |

Der Simulator implementiert die Schnittstelle zur Maschinendatenerfassung. Die Modulgrenzen unterstützen künftige Datenquellenadapter, Web- und Desktop-Clients sowie eine spätere Auslagerung von Analysediensten. [Systemarchitektur](docs/architecture/ARCHITECTURE.md)

## Lokal einrichten und starten

Verwenden Sie das in [global.json](global.json) festgelegte .NET-10-SDK. Derzeit gilt Version 10.0.404 mit Patch-Updates innerhalb desselben SDK-Featurebands. Führen Sie die Befehle in PowerShell aus dem Repository-Stammverzeichnis aus. Unter Windows können Sie bei einer lokalen SDK-Installation unter `.dotnet/` statt `dotnet` den Pfad `.\.dotnet\dotnet.exe` verwenden.

```powershell
dotnet restore FactoryPulseAI.slnx
dotnet build FactoryPulseAI.slnx --no-restore
```

Für die lokale SQLite-Datenbank stellen Sie das im Repository versionierte EF-Werkzeug wieder her und führen die Migration explizit aus. Das Datenbankverzeichnis muss beschreibbar sein. [Datenbank und Migrationen](docs/architecture/DATABASE.md)

```powershell
dotnet tool restore
dotnet ef database update --project src/FactoryPulse.Infrastructure --startup-project src/FactoryPulse.Api --context FactoryPulseDbContext
```

Richten Sie bei Bedarf das ASP.NET-Core-Entwicklungszertifikat als vertrauenswürdig ein und starten Sie das HTTPS-Profil. Für den Start aus der IDE benötigen Sie Visual Studio 2026 mit dem Workload **ASP.NET und Webentwicklung**. Öffnen Sie [FactoryPulseAI.slnx](FactoryPulseAI.slnx), legen Sie `FactoryPulse.Api` als Startprojekt fest, wählen Sie **FactoryPulse.Api (HTTPS)** und drücken Sie **F5**.

```powershell
dotnet dev-certs https --trust
dotnet run --project src/FactoryPulse.Api --launch-profile "FactoryPulse.Api (HTTPS)" --no-build
```

| Lokaler Endpunkt | Zweck |
| --- | --- |
| `https://localhost:7161/health` | Erreichbarkeit des API-Prozesses |
| `https://localhost:7161/api/status` | Instanzname, Simulationsmodus und UTC-Zeitstempel der Abfrage |
| `https://localhost:7161/openapi/v1.json` | OpenAPI-Dokument in der Entwicklungsumgebung |

Legen Sie ein lokales Konto an, starten Sie die API und öffnen Sie ein zweites Terminal für das Dashboard. Verwenden Sie Node.js 24 und melden Sie sich unter `http://localhost:4200` an. [Konto anlegen](docs/architecture/AUTHENTICATION.md) · [Dashboard einrichten](docs/architecture/WEB.md)

```powershell
Set-Location src/FactoryPulse.Web
npm ci
npm start
```

## Konfiguration und Sicherheit

ASP.NET Core lädt Standardwerte aus `appsettings.json` und validiert typisierte Einstellungen beim Start. Mit `Database__Path` wählen Sie eine beschreibbare SQLite-Datei, mit `ApplicationStatus__InstanceName` den Instanznamen und mit `AllowedHosts` die zulässigen Hostnamen. Verwenden Sie für Zugangsdaten in der Entwicklung .NET User Secrets und bei der Bereitstellung geschützte Umgebungsvariablen oder einen Secret-Provider. Zugangsdaten gehören nicht in die Versionsverwaltung.

Führen Sie die Datenbankmigration aus und setzen Sie vor dem API-Start `Simulation__Enabled=true`, um simulierte Messwerte zu speichern. [Simulatoreinrichtung und Einstellungen](docs/architecture/SIMULATION.md)

Unter `AlarmDetection` lassen sich Auslöse- und Rücksetzgrenzen einstellen. Alarme können mit der Rolle Operator oder Administrator quittiert werden. [Grenzwerte](docs/architecture/ALARMS.md) · [Zugriffsregeln](docs/architecture/AUTHENTICATION.md)

## Automatisierte Tests und Qualitätssicherung

Die xUnit-Tests prüfen Fachregeln, die HTTP-Pipeline mit WebApplicationFactory sowie Migrationen und Abfragen mit isolierten SQLite-Datenbanken. Die Tests laufen auf Microsoft.Testing.Platform. Coverlet erfasst die Testabdeckung. Zentrale Paketversionierung, Nullable Reference Types, .NET-Analyzer und `dotnet format` sorgen für einheitliche Build-Prüfungen. [Technische Anforderungen](docs/REQUIREMENTS.md)

```powershell
dotnet test --solution FactoryPulseAI.slnx --no-build --coverlet
dotnet format FactoryPulseAI.slnx --verify-no-changes --no-restore
```

## Implementierungsstand

| Funktion | Status |
| --- | --- |
| Anwendungsstatus, Prüfung des API-Prozesses und OpenAPI in der Entwicklungsumgebung | Implementiert |
| Modelle für Maschinen, Messwerte und Alarme sowie SQLite-Persistenz | Implementiert |
| Simulierte Maschinentelemetrie und gebündelte Speicherung | Implementiert |
| Schreibgeschützte REST-API für Maschinenbestand, Statusübersicht und Messwerte | Implementiert |
| Temperatur- und Schwingungsalarme, Alarmlebenszyklus und REST-Abfragen | Implementiert |
| Authentifizierter SignalR-Stream für Messwerte und Alarmänderungen | Implementiert, für Production explizit aktivierbar |
| Identity-Cookie-Sitzungen und rollenbasierter Zugriff | Implementiert |
| Angular-Dashboard für die Maschinenüberwachung auf Deutsch und Englisch | Implementiert |
| Prüfungen für API und Web-Client mit GitHub Actions | Konfiguriert |
| WPF-Bedienoberfläche | Geplant |
| Qualitätsprüfung, vorausschauende Analysen und automatisierte Bereitstellung | Geplant |

## Roadmap

- **Telemetrie und Diagnose.** OPC UA für den lesenden Zugriff prüfen und weitere Datenquellen über Plugins anbinden.
- **Bedienoberflächen.** Einen WPF-Desktop-Client für Maschinenzustände, Trends und Alarme entwickeln.
- **Qualitätsprüfung und Analysen.** Kamerabasierte Qualitätsprüfung mit OpenCV ergänzen und vorausschauende Wartung mit ML.NET evaluieren.
- **Bereitstellung und Betrieb.** Docker und Docker Compose ergänzen und Kubernetes für die Orchestrierung evaluieren.

[Grundlegende Architekturentscheidung](docs/adr/0001-initial-architecture.md)
