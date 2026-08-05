# Code Review: Flowery.NET.Kanban-Port

## Bewertung

✅ **Technisch abgeschlossen einschließlich Dialogprüfung (2026-08-02)**

Die zwölf priorisierten Findings, die neun ausdrücklich genannten Testlücken und die nachträglich erkannte Dialoglücke sind abgearbeitet. Der vollständige Testlauf `review-dialog-final-tests.log` besteht mit `294/294`; `review-dialog-final-desktop-build.log` baut sechs Desktopprojekte und `review-dialog-final-build-all.log` Desktop, Browser und Android in `10/10` Schritten, jeweils ohne Warnungen oder Fehler. Es wurde nichts gestaged oder committet.

## Nachgezogene Dialogprüfung und UI-Korrekturen

**Status: ✅ Erledigt (2026-08-02)**

**Abdeckung:** Die Tests öffnen alle 13 Dialogklassen über ihre produktiven Pfade: Board-, Task- und Settings-Editor einschließlich Lane-Delete, Input-, Column-, Confirm-, Keyboard-Help- und Metrics-Dialog sowie GitHub-Connect, Provider-Error, Provider-Confirm und Link-Local-User. Geprüft werden Modal-Lifecycle, Host-Close und Unload, Save/Cancel-Isolation, Escape, Fokuswiederherstellung, zyklische Tab-Navigation, verschachtelte Dialoge, Subtask-Escape sowie reale Footer- und Content-Layouts.

**Behobene Befunde:** `FlowKanbanDialogBase` exponiert die Dialogwurzel jetzt als benanntes UIA-Window-/Content-Element. `DaisyPopover` reagiert für Button-Trigger auf den gemeinsamen Click-Pfad, und die vier Board-Aktionen besitzen lokalisierte Namen sowie ein ausführbares `IInvokeProvider` statt einer wirkungslosen Auswahlsemantik. Im Task-Editor liegen die vier DatePicker ohne Überlappung untereinander; gemeinsame Scrollinhalte reservieren 24 Pixel rechts, der Divider direkt über „Scheduling“ ist entfernt, und ein Sprachwechsel verändert nicht mehr die regionale `CurrentCulture`. Toolbar-Icons und der Statusbar-Eintrag „Show archive“ verwenden die gemessenen gemeinsamen Größen- und Zentrierungsregeln.

**Echte UI-Evidenz:** Mit gesetztem `AVALONIA_TELEMETRY_OPTOUT=1` und `DOTNET_CLI_TELEMETRY_OPTOUT=1` öffnete UIA `Board actions` und danach `Edit Board`; der reale Dialog erscheint als `Window "Board Properties"`. Seine 20-Pixel-Scrollbar beginnt 10 Pixel hinter den 445 Pixel breiten Eingabeflächen, Save und Cancel messen jeweils 100 × 40 Pixel. Der reale Task-Editor zeigt für die aktuelle Windows-Region `day | month | year`, gibt rechts Platz für die Scrollbar frei und besitzt keinen Divider über „Scheduling“. Standard-, Compact- und Swimlane-Templates sowie Toolbar und Statusbar wurden mit den vorhandenen gemeinsamen Geometrietests gemessen.

**Regressionen:** `dialog-ui-focused-final-test.log` prüft Dialoge, Popover, regionale Formatkultur und Board-Menü-Semantik gemeinsam (`15/15`). `scheduling-divider-test.log` schützt DatePicker-Flächen, Scrollbar-Abstand und den entfernten Scheduling-Divider. Die vollständige Suite endet mit `294/294`.

**Verbleibendes Validierungsrisiko:** Ein zusätzlicher realer Pointer-Drag am Dialog-Resize-Grip konnte wegen der Windows-Foreground-Sperre nicht wiederholt werden. Die priorisierte Spalten-Resize-Geometrie ist davon unabhängig über die echten Standard-/Compact-/Swimlane-Templates und Pointer-/Tastaturtests abgedeckt.

## P0 – vor jedem Commit beheben

### 1. Jede FlowKanban-Instanz teilt standardmäßig dasselbe Board

**Status: ✅ Erledigt (2026-08-02)**

**Evidenz:** `BoardProperty` besitzt keinen veränderbaren statischen Defaultwert mehr. Der Konstruktor setzt vor den Loaded-Handlern mit `SetCurrentValue` ein neues Board je Instanz; der Property-Vertrag koerziert Binding-`null` und der öffentliche Setter weist `null` zurück. `When_Constructing_MultipleKanbans_DefaultBoardsAreIsolated` prüft getrennte Referenzen, Collections und Board-/Collection-Events.

**Befund**

- [Controls/FlowKanban.cs](Controls/FlowKanban.cs#L245) registriert BoardProperty mit einer bereits erzeugten FlowKanbanData-Instanz als statischem Defaultwert.
- Avalonia-Property-Metadaten sind statisch. Zwei neue FlowKanban-Controls erhalten deshalb zunächst dieselbe veränderbare Board-Instanz.
- Der Konstruktor erzeugt zwar instanzbezogene Collections für LaneRows und Boards, aber kein instanzbezogenes Board.

**Auswirkung**

Änderungen am Default-Board einer Instanz können in einer zweiten Instanz erscheinen. Event-Tracking, Autosave und LastBoardId können dadurch Daten der falschen Control-Instanz verarbeiten.

**Aktion**

1. Kein veränderbares Objekt als Avalonia-Defaultwert registrieren.
2. Board pro Instanz im Konstruktor initialisieren, bevor Tracking oder Loaded-Logik aktiv werden; dabei Binding-Prioritäten beachten und SetCurrentValue verwenden, wenn ein vorhandenes Binding nicht ersetzt werden darf.
3. Festlegen, ob null für die öffentliche Property zulässig ist. Falls nicht, dies im Property-Vertrag und Setter erzwingen.

**Pflichttest**

- Zwei FlowKanban-Instanzen erzeugen.
- Eine Spalte nur dem ersten Board hinzufügen.
- Prüfen, dass Referenzen, Spalten und Events vollständig getrennt bleiben.

### 2. Persistenz meldet Erfolg, obwohl Schreiben, Umbenennen oder Löschen fehlgeschlagen sein kann

**Status: ✅ Erledigt (2026-08-02)**

**Evidenz:** Der `IStateStorage`-Vertrag verlangt jetzt die Weitergabe mutierender Fehler. Desktop- und Browser-Backend verschlucken Save-/Rename-/Delete-Exceptions nicht mehr; Rename meldet einen fehlenden temporären Schlüssel. BoardStore liefert Save-, Rename- und Delete-Fehler als `false` mit der konkreten Exception. `FlowKanbanManager.PersistenceFailed` und die sichtbare Statuszeile erhalten denselben Fehler. Fünf P0-Regressionstests sowie die vollständige Testsuite (`255/255`) bestehen; der Desktop-Build einschließlich Tests endet mit `0` Warnungen und `0` Fehlern.

**Befund**

- [FileStateStorage.cs](../Flowery.NET/Services/FileStateStorage.cs#L38) verschluckt alle Exceptions in SaveLines, Delete und Rename.
- [BrowserStateStorage.cs](../Flowery.NET.Gallery.Browser/BrowserStateStorage.cs#L36) macht dasselbe für localStorage.
- [FlowKanbanBoardStore.cs](Controls/FlowKanbanBoardStore.cs#L90) schreibt zuerst einen temporären Schlüssel, ruft danach Rename auf und gibt anschließend bedingungslos true zurück.
- TryDeleteBoard gibt ebenfalls true zurück, wenn Delete intern fehlgeschlagen ist.

**Auswirkung**

Die UI kann „gespeichert“ melden, LastBoardId aktualisieren und den Benutzer weiterarbeiten lassen, obwohl das Board nicht persistiert wurde. Das ist ein realer Datenverlustrisiko. Das temporäre-Schlüssel-Verfahren ist unter diesem Vertrag nicht atomar, weil weder Write noch Rename Erfolg signalisieren.

**Aktion**

1. Den IStateStorage-Vertrag so ändern, dass mutierende Operationen entweder Exceptions weitergeben oder ein eindeutiges Resultat liefern.
2. Save, Rename und Delete nicht in den Backends verschlucken.
3. TrySaveBoard erst nach erfolgreichem Write und erfolgreichem Rename mit true beenden.
4. Optional nach Rename verifizieren, dass der Zielschlüssel lesbar ist; mindestens muss ein fehlender temporärer Schlüssel als Fehler gelten.
5. Persistenzfehler über PersistenceFailed und die sichtbare Statusmeldung bis zum Benutzer weiterreichen.

**Pflichttests**

- Ein Storage-Fake, dessen SaveLines fehlschlägt.
- Ein Storage-Fake, dessen Rename fehlschlägt.
- Ein Storage-Fake, dessen Delete fehlschlägt.
- In allen Fällen müssen TrySaveBoard bzw. TryDeleteBoard false und eine konkrete Exception liefern.

## P1 – vor Merge beheben

### 3. Austauschbare Collections und Event-Tracking haben widersprüchliche Besitzregeln

**Status: ✅ Erledigt (2026-08-02)**

**Evidenz:** `Columns`, `Tasks` und `Subtasks` melden Collection-Austausch nun über Backing Fields und `PropertyChanged`. `FlowKanban` und `FlowTaskCard` halten die jeweils konkret abonnierte Collection fest, lösen alte Collection-/Item-Handler und binden die neue Collection einschließlich Reset-Fällen vollständig an. Drei Regressionstests tauschen die Collections nach dem Attach aus und belegen, dass nur neue Collections UI, Suche, Auswahl-/WIP-Metriken, Subtask-Anzeige und Autosave beeinflussen.

**Befund**

- [FlowKanbanModels.cs](Controls/FlowKanbanModels.cs#L884) definiert Columns als frei austauschbare Auto-Property ohne PropertyChanged.
- [FlowKanbanModels.cs](Controls/FlowKanbanModels.cs#L325) macht dasselbe für Subtasks.
- Tags und Lanes verwenden dagegen Backing Fields und PropertyChanged. Tasks besitzt ebenfalls einen Setter mit eigener CollectionChanged-Umschaltung.
- [FlowKanban.cs](Controls/FlowKanban.cs#L1735) bindet Event-Handler an die zu diesem Zeitpunkt vorhandenen Collections.
- Die Board-Logik enthält sogar einen Zweig für PropertyChanged(nameof(FlowKanbanData.Columns)), den die aktuelle Columns-Property nie auslöst.
- Beim Austausch von FlowKanbanColumnData.Tasks wird die interne WIP-Subscription umgestellt, die Subscription des übergeordneten FlowKanban bleibt jedoch an der alten Collection.

**Auswirkung**

Nach einem Collection-Austausch reagieren Suche, Auswahlmetriken, Autosave, WIP-Status und Subtask-Anzeige teilweise weiter auf alte Daten oder gar nicht mehr auf neue Daten.

**Aktion**

Eine einzige Besitzregel wählen und überall anwenden:

1. Entweder Collections bleiben über die gesamte Objektlebenszeit identisch und sind nur lesbar austauschbar; Deserialisierung füllt die bestehenden Collections.
2. Oder jeder Collection-Setter meldet Old/New und alle Besitzer schalten ihre CollectionChanged- und Item-Subscriptions vollständig um.
3. Columns, Tasks, Subtasks, Tags und Lanes nach demselben Muster implementieren.
4. RebuildTracking nicht als Ersatz für fehlende Old-Collection-Information verwenden.

**Pflichttests**

- Columns, Tasks und Subtasks jeweils nach bereits erfolgtem Attach austauschen.
- Danach Änderungen an alter und neuer Collection ausführen.
- Nur die neue Collection darf UI, Suche, Metriken und Autosave beeinflussen.

### 4. Asynchrones Laden benutzerspezifischer Einstellungen ist ein Race und der Try-Vertrag ist falsch

**Status: ✅ Erledigt (2026-08-02)**

**Evidenz:** `LoadUserSettingsAsync` ist die zentrale awaitbare Operation mit `CancellationToken`, monotoner Version und Provider-Snapshot. Providerwechsel und Unload invalidieren und beenden vorherige Läufe; nach asynchronen Provideraufrufen werden Version und Provider im Avalonia-Dispatcher erneut geprüft. Der synchrone Try-Pfad startet keine unbeobachtete Arbeit mehr, Exceptions erreichen `PersistenceFailed` und die sichtbare Statusmeldung. Kontrollierte Tests decken umgekehrte Abschlussreihenfolge, Provider-Exception und verspäteten Abschluss nach Unload ab (`3/3`).

**Befund**

- [FlowKanban.cs](Controls/FlowKanban.cs#L1336) startet EnsureUserSettingsLoadedAsync ohne Await und gibt sofort true zurück.
- Exceptions aus dem asynchronen Teil erreichen den out-Parameter error nicht.
- [FlowKanban.cs](Controls/FlowKanban.cs#L1400) besitzt weder CancellationToken noch Versionsprüfung noch eine Provider-Identitätsprüfung nach dem Await.
- [FlowKanban.Users.cs](Controls/FlowKanban.Users.cs#L47) startet bei Provider-Wechseln mehrere dieser Operationen parallel.
- Die Assignee-Aktualisierung besitzt bereits eine Versionsprüfung. Die Settings-Aktualisierung löst dasselbe Problem erneut, aber ohne diese Absicherung.

**Auswirkung**

Ein langsamer alter Provider kann nach einem schnellen neuen Provider fertig werden und dessen Settings überschreiben. Beim Unload kann ein später Abschluss weiter Control-Zustand verändern. Provider-Exceptions werden als erfolgreicher TryLoad-Aufruf ausgegeben.

**Aktion**

1. Eine zentrale, awaitbare LoadUserSettingsAsync-Operation einführen.
2. Bei Provider-Wechsel und Unload vorherige Läufe abbrechen oder über eine monoton steigende Version ungültig machen.
3. Nach jedem Await prüfen, ob Provider und Version noch aktuell sind.
4. UI-Zustand ausschließlich auf dem Avalonia-Dispatcher anwenden.
5. Fehler beobachten und über den vorhandenen Persistenzfehlerkanal melden.
6. Den synchronen TryLoad-Vertrag entweder wirklich synchron halten oder durch einen ehrlichen asynchronen Result-Vertrag ersetzen.

**Pflichttests**

- Zwei kontrollierte Provider liefern in umgekehrter Reihenfolge.
- Der alte Provider darf den Zustand nicht überschreiben.
- Ein werfender Provider muss als Fehler sichtbar werden.
- Ein Abschluss nach Unload darf keine UI-Änderung ausführen.

### 5. Uno-Plattformsymbole wurden unverändert kopiert und sind in diesem Projekt wirkungslos

**Status: ✅ Erledigt (2026-08-02)**

**Evidenz:** `FlowKanbanPlatformDefaults` bestimmt Desktop-, Browser-, Android- und iOS-Capabilities zur Laufzeit und liefert die Defaults für gestaffeltes Rendering, Keyboard-Hilfe und Spalten-Tooltips. Die Uno-Präprozessorsymbole wurden aus `FlowKanban.UI.cs`, `FlowKanbanColumn.cs` und `FlowKanban.Drop.cs` entfernt; eine Repository-Suche findet außerhalb dieser historischen Arbeitsliste keine Uno-/Skia-Symbole mehr. Ein Theory-Test prüft alle vier Plattformprofile einschließlich der abweichenden Browser- und Mobile-Werte (`4/4`).

**Befund**

- [FlowKanban.UI.cs](Controls/FlowKanban.UI.cs#L172) verwendet __ANDROID__ und __IOS__.
- [FlowKanban.UI.cs](Controls/FlowKanban.UI.cs#L432) verwendet __WASM__ und __ANDROID__ für Keyboard-Hilfe und Tooltips.
- [Flowery.NET.Kanban.csproj](Flowery.NET.Kanban.csproj) kompiliert die Bibliothek als net10.0. Sie wird nicht separat unter den Uno-Head-Symbolen gebaut.

**Auswirkung**

Die Plattformzweige sind im aktuellen Avalonia-Paket tot. Browser erhält Desktop-Defaults; Android/iOS erhalten unter anderem nicht den vorgesehenen gestaffelten Renderingmodus. Das kann bei dem vorgesehenen Stresstest mit mehreren hundert Karten direkt relevant werden.

**Aktion**

1. Plattformverhalten über einen zur Laufzeit ausgewerteten Capability-/Platform-Service oder über vom Host gesetzte Properties bestimmen.
2. Keine Uno-spezifischen Präprozessorsymbole im net10.0-Avalonia-Projekt behalten.
3. Browser-, Mobile- und Desktop-Defaults isoliert testbar machen.

### 6. Die komplette Spaltenoberfläche ist viermal kopiert

**Status: ✅ Erledigt (2026-08-02)**

**Evidenz:** Header, Grip, Aktionen, Collapse-Flächen, Add-Card-Bereiche, Taskliste, Card-Template und Drop-Indikator liegen jetzt einmalig im typbasierten `FlowKanbanColumn`-`ControlTheme`. Standard, Compact, Swimlane-Header und Swimlane-Zelle bestehen nur noch aus vier konfigurierten Control-Instanzen; die Swimlane-Zelle setzt zusätzlich ihre Listenbegrenzung. Der Headless-Test lädt dieselbe Theme-Reihenfolge wie die Gallery und misst in allen drei Layoutmodi Buttonmaße, Iconzentrierung, Collapse-Flächenfreigabe und Resize-Geometrie am echten Template.

**Befund**

- [DaisyKanban.axaml](Themes/DaisyKanban.axaml#L1525), [DaisyKanban.axaml](Themes/DaisyKanban.axaml#L1806), [DaisyKanban.axaml](Themes/DaisyKanban.axaml#L2052) und [DaisyKanban.axaml](Themes/DaisyKanban.axaml#L2342) enthalten vier große Varianten derselben FlowKanbanColumn-Struktur.
- Header, Grip, Toolbuttons, Collapse-Schaltflächen, Taskliste, Drop-Indikator, Card-Template und Add-Card-UI werden wiederholt.
- Eigenschaften wie ShowColumnHeader, ShowTasks, ShowAddCard und IsDropEnabled existieren bereits, werden aber nicht genutzt, um eine einzige Darstellung zu tragen.

**Auswirkung**

Jede Größen-, Icon-, Automation-, Collapse- oder Drag-and-drop-Korrektur muss mehrfach identisch ausgeführt werden. Die bereits aufgetretenen Unterschiede bei Buttonmaßen und Collapse-Verhalten sind genau die Art Fehler, die diese Struktur begünstigt.

**Aktion**

1. Den visuellen Aufbau vollständig in ein einziges FlowKanbanColumn-ControlTheme bzw. eine wiederverwendbare Column-View verlagern.
2. Standard, Compact, Swimlane-Header und Swimlane-Zelle nur über Datenkontext und vorhandene Sichtbarkeits-/Verhaltensproperties konfigurieren.
3. Gemeinsame Card- und Add-Card-Templates ebenfalls nur einmal definieren.
4. Erst nach der Konsolidierung weitere Maßkorrekturen vornehmen, damit Tests eine einzige Implementierung absichern.

### 7. LastModified wird gelesen, aber nie geschrieben

**Status: ✅ Erledigt (2026-08-02)**

**Evidenz:** `FlowKanbanData.UpdatedAt` wird persistiert; `FlowKanbanBoardStore` setzt den Wert über einen injizierbaren `TimeProvider` für jeden Save und behält ihn nur nach erfolgreichem Write/Rename bei. Bei Fehlern wird der vorherige Wert wiederhergestellt. Metadaten verwenden für Altdateien weiterhin `CreatedAt` als Fallback. Deterministische Tests belegen Roundtrip, Fehlerrücksetzung und dass ein später bearbeitetes älteres Board in der Recent-Sortierung nach vorn rückt. P1-Abschluss: gezielte Tests `15/15`, vollständige Suite `268/268`, Desktop-Build `6/6` mit `0` Warnungen und `0` Fehlern.

**Befund**

- [FlowKanbanBoardSanitizer.cs](Controls/FlowKanbanBoardSanitizer.cs#L75) bevorzugt ein JSON-Feld updatedAt für FlowBoardMetadata.LastModified.
- [FlowKanbanModels.cs](Controls/FlowKanbanModels.cs#L748) besitzt kein UpdatedAt-Feld; es existiert nur CreatedAt.
- [FlowKanbanBoardStore.cs](Controls/FlowKanbanBoardStore.cs#L90) aktualisiert vor dem Speichern keinen Änderungszeitpunkt.

**Auswirkung**

Die Home-Sortierung „zuletzt geändert“ sortiert faktisch nach Erstellzeit oder DateTime.MinValue. Umbenennen und Bearbeiten ändern die angezeigte Zeit nicht.

**Aktion**

1. UpdatedAt als persistiertes Modellfeld einführen oder den Zeitstempel verlässlich aus dem Storage beziehen.
2. Bei jeder erfolgreichen Board-Speicherung aktualisieren.
3. Migration für bestehende Dateien definieren: Fallback auf CreatedAt.

**Pflichttest**

- Zwei Boards mit unterschiedlicher Erstellzeit speichern, das ältere danach ändern und prüfen, dass es in „zuletzt geändert“ vorne steht.

## P2 – gezielt nach P0/P1 bearbeiten

### 8. Die UI-Automation deckt Namen und IDs ab, aber nicht die Semantik eigener interaktiver Controls

**Status: ✅ Erledigt (2026-08-02)**

**Evidenz:** `FlowKanbanColumnsHostAutomationPeer` exponiert `IRangeValueProvider` mit Wert, Minimum, Maximum, 8-/32-Pixel-Schritten und Read-only-Status; die echte Standard- und Swimlane-Oberfläche bindet einen in allen zwölf Kanban-Sprachen vorhandenen Tastatur-Hilfetext. Karten verwenden `IInvokeProvider` für dieselbe Bearbeiten-Aktion wie Enter/Doppelklick. Nur Spaltenoberflächen mit realer Collapse-Aktion verwenden `IExpandCollapseProvider`; Compact-Spalten und reine Swimlane-Zellen erhalten keinen erfundenen Collapse-Provider. Avalonia 12.1 stellt keinen Drag-Provider-Vertrag bereit, daher wurde kein inkompatibles Pattern erfunden. Eine gemeinsame Visual-Tree-Prüfung schließt versteckte und entfernte Template-Instanzen aus. Die nachgezogene Dialogprüfung ergänzt benannte UIA-Window-Elemente für alle Dialoge sowie echte `MenuItem`-Invoke-Provider für die vier Board-Aktionen; die reale UIA-Kette öffnet damit `Board actions`, `Edit Board` und anschließend das Dialog-Window. `p2-uai-tests-targeted-after-visibility.log` prüft die ursprünglichen Provider, Werte, Aktionen, HelpText und Layoutwechsel gegen die echten Templates (2/2); `dialog-ui-focused-final-test.log` ergänzt Dialog- und Board-Menü-Semantik (`15/15`).

**Befund**

- Es gibt keine eigene AutomationPeer-Implementierung im Kanban-Projekt.
- [FlowKanbanColumnsHost.cs](Controls/FlowKanbanColumnsHost.cs#L47) ist fokussierbar und per Pfeil-, Home-, End- und Page-Tasten veränderbar, setzt aber nur AutomationProperties.Name.
- FlowTaskCard und FlowKanbanColumn erhalten Name und AutomationId, exponieren aber keine semantischen Patterns für Auswahl, Invoke, Drag oder die aktuelle Spaltenbreite.
- Die aktuellen Tests prüfen hauptsächlich, ob Strings und IDs vorhanden sind.

**Auswirkung**

UAI-Clients können Elemente finden, wissen aber bei den eigenen interaktiven Controls nicht zuverlässig, welche Aktion möglich ist oder welchen Wert eine Größenänderung aktuell hat.

**Aktion**

1. Für FlowKanbanColumnsHost einen passenden AutomationPeer mit aktuellem Wert, Minimum, Maximum und Read-only-Status bereitstellen; zusätzlich eine lokalisierte HelpText-Beschreibung der Tastaturbedienung.
2. Für interaktive Karten und Spalten festlegen, ob Invoke-, Selection- oder Drag-Semantik angeboten wird, und entsprechende Peers implementieren.
3. Versteckte Layoutvarianten aus dem Automation-Tree ausschließen.
4. Tests gegen AutomationPeers und Provider-Patterns schreiben, nicht nur gegen Attached-Property-Strings.

### 9. FlowKanbanContentControl implementiert unvollständige Button-Semantik für jedes Kanban-Control

**Status: ✅ Erledigt (2026-08-02)**

**Evidenz:** Die vollständige Referenzsuche über C#, AXAML und Dokumentation fand keinen Consumer der geerbten `Command`-, `CommandParameter`- oder `Click`-API. Die Member und die unvollständigen Pointer-Handler wurden deshalb gemäß Finding ohne Kompatibilitätswrapper aus `FlowKanbanContentControl` entfernt; spezifische Karten-Commands und konkrete `DaisyButton`-Flächen bleiben unverändert. `When_InspectingKanbanContainerBase_ButtonContractIsAbsent` schützt die Entfernung, und der echte Add-Card-Template-Test führt dessen `IInvokeProvider` aus. `p2-content-control-tests-targeted.log`: 2/2 bestanden; `p2-content-control-build.log`: 6/6 Projekte, 0 Warnungen, 0 Fehler.

**Befund**

- [FlowKanbanContentControl.cs](Controls/FlowKanbanContentControl.cs#L21) setzt bei linkem PointerPressed ein Flag und führt bei jedem späteren PointerReleased Click und Command aus.
- Release-Button, Release-Position und Bewegungsdistanz werden nicht geprüft; Pointer-Capture wird nicht aktiv übernommen.
- Von dieser Basis erben auch FlowKanban, FlowKanbanHome, FlowKanbanColumn und FlowKanbanUserManagement, obwohl sie keine Buttons sind.

**Auswirkung**

Die öffentliche Command-/Click-API kann nach Drag-, Child-Control- oder Release-Abläufen unerwartet ausgelöst werden und liefert UAI zugleich keine echte Button-Semantik.

**Aktion**

1. Command und Click aus der allgemeinen Container-Basisklasse entfernen, wenn kein konkreter Consumer sie benötigt.
2. Für tatsächlich klickbare Flächen Button/DaisyButton oder ein eigenes, vollständig implementiertes Invoke-Control verwenden.
3. Falls die API bleiben muss: Pointer-Capture, linker Release, Bewegungsgrenze, IsPointerOver, IsEnabled und Keyboard-Aktivierung vollständig abbilden.

### 10. Lokalisierungsregistrierung ist global veränderbar, nicht threadsicher und verschluckt Fehler

**Status: ✅ Erledigt (2026-08-02)**

**Evidenz:** `RegisterAssembly` schützt Assembly-, Resource- und Writer-Dictionaries nun mit einem gemeinsamen Lock und bleibt bei parallelen Wiederholungen idempotent. Erst nach dem vollständigen Laden aller Sprachressourcen wird ein tief kopierter, per `Volatile.Write` publizierter Read-Snapshot ersetzt; `GetStringInternal`, `CurrentCulture` und `IsRtl` lesen atomar ohne Registrierungs-Lock. JSON-/Stream-Fehler werden mit Ressource, Exceptiontyp und Meldung über `Trace.TraceError` ausgegeben. Der Regressionstest bettet gültiges Englisch und absichtlich defektes Deutsch in die echte Testassembly ein, führt 512 parallele Registrierungs-/Lookup-Operationen aus, prüft Fallback und genau eine protokollierte `JsonException`. `p2-localization-tests-class-final.log`: 95/95 bestanden; `p2-localization-build-isolated-tests.log`: 6/6 Projekte, 0 Warnungen, 0 Fehler.

**Befund**

- [FloweryLocalization.cs](../Flowery.NET/Localization/FloweryLocalization.cs#L48) hält globale Dictionary- und HashSet-Instanzen.
- RegisterAssembly verändert diese Strukturen ohne Synchronisierung.
- Kanban registriert dieselbe Assembly aus mehreren statischen Basiskonstruktoren.
- GetStringInternal und LoadTranslation fangen alle Exceptions und geben Schlüssel zurück. Ein Race oder defektes Resource-JSON wird dadurch als fehlende Übersetzung maskiert.

**Auswirkung**

Parallele erste Verwendung verschiedener Kanban-Typen kann inkonsistente Registrierung erzeugen. Fehler sind diagnostisch nicht unterscheidbar von tatsächlich fehlenden Keys.

**Aktion**

1. Registrierung atomar und idempotent unter einer gemeinsamen Synchronisierung durchführen.
2. Für Reads nach der Initialisierung unveränderliche Snapshots verwenden.
3. Resource-Fehler mindestens über den vorhandenen Logging-Kanal melden.
4. Parallele Registrierung und paralleles Lesen testen.

### 11. FlowKanbanColumnsHost durchsucht bei jedem Layout die Visual Tree

**Befund**

- [FlowKanbanColumnsHost.cs](Controls/FlowKanbanColumnsHost.cs#L47) hängt dauerhaft an LayoutUpdated.
- OnLayoutUpdated ruft GetVisualDescendants().OfType&lt;StackPanel&gt;().FirstOrDefault() auf, nur um Spacing erneut zu setzen.

**Auswirkung**

Bei vielen Karten und häufigen Layoutzyklen entsteht unnötige Arbeit auf dem UI-Thread. Der Stresstest mit mehreren hundert Karten sollte diesen Pfad messen.

**Aktion**

1. Das erzeugte Panel direkt speichern oder beim Template-/Container-Aufbau einmal auflösen.
2. Spacing nur bei ColumnSpacing- oder Template-Änderung setzen.
3. Einen Stresstest mit Realisierungszahl, Layoutdauer und wiederholten Collapse-/Resize-Vorgängen ergänzen.

**Status: ✅ Erledigt (2026-08-02)**

**Evidenz:** `FlowKanbanColumnsHost` speichert das vom `ItemsPanel`-Template erzeugte `StackPanel` direkt, initialisiert dessen Abstand bei der Erzeugung und aktualisiert ihn danach ausschließlich bei `ColumnSpacing`-Änderungen. Der dauerhafte `LayoutUpdated`-Handler und die Visual-Tree-Suche sind entfernt. `When_SharedColumnSurfaceRealizes_AllLayoutsKeepMetricsAndGeometry` prüft Panelidentität, Abstandsänderung und Collapse-Geometrie am echten Template. `When_GeneratedDemoBoardIsStressed_AllOperationsRemainBoundedAndPersistent` verwendet den vorhandenen `CreateDemoBoardCommand` für 200 Karten und misst Laden, Realisierung, Suche, Scrollen, 20 Collapse-/Resize-Zyklen, Drag-and-drop auf einen realisierten Container, Autosave und Wiederladen. `test-gaps-generated-stress-targeted.log` belegt den Stresstest; `review-final-build-all.log` belegt den abschließenden 10/10-Build ohne Warnungen oder Fehler.

### 12. Sichtbare und kommentierte Uno-/WinUI-Reste sind noch vorhanden

**Status: ✅ Erledigt (2026-08-02)**

**Evidenz:** Die rein verwalteten lokalen und zusammengesetzten Provider tragen keine unzutreffenden Plattformattribute mehr; dadurch entfallen alle drei Uno-begründeten `CA1416`-Suppressions. Tote `HAS_UNO_SKIA`-Zweige und rendererbezogene Altkommentare wurden entfernt oder anhand des tatsächlichen Avalonia-Verhaltens formuliert. Die ausdrücklich geforderte öffentliche Umbenennung von `ContentDialogChrome` zu `OverlayChromePadding` ist über alle fünf internen Verbraucher geprüft und erfolgt ohne Kompatibilitätswrapper. `Kanban_Users_SingleUserNotice` ersetzt den sichtbaren englischen Literaltext und ist in allen zwölf vorhandenen Kanban-Ressourcen nichtleer; ein Ressourcentest prüft dies gegen die eingebetteten JSON-Dateien. `p2-platform-cleanup-build.log` belegt 6/6 Projekte bei 0 Warnungen und 0 Fehlern; `p2-platform-cleanup-tests-targeted.log` belegt 2/2 Tests am echten Spaltentemplate und an den Lokalisierungsressourcen.

**Befund**

- [FlowKanban.Users.cs](Controls/FlowKanban.Users.cs#L64), [FlowKanbanUserManagement.cs](Controls/FlowKanbanUserManagement.cs#L693) und [FlowKanbanColumn.cs](Controls/FlowKanbanColumn.cs#L232) beschreiben weiterhin Uno-Heads bzw. Uno/Skia.
- [DaisyKanban.axaml](Themes/DaisyKanban.axaml#L27) beschreibt WinUI-Innenabstände.
- [FlowKanbanDialogBase.cs](Controls/FlowKanbanDialogBase.cs#L17) verwendet mit ContentDialogChrome einen WinUI-spezifischen Namen für Avalonia-Overlaymaße.
- [DaisyKanban.axaml](Themes/DaisyKanban.axaml#L650) enthält einen sichtbaren englischen Hinweistext außerhalb der Lokalisierungsressourcen.

**Auswirkung**

Kommentare und Namen erklären nicht mehr die tatsächlich laufende Plattform. Der nicht lokalisierte Text durchbricht die ansonsten mehrsprachige Oberfläche.

**Aktion**

1. Plattformkommentare anhand des Avalonia-Verhaltens neu formulieren oder entfernen.
2. Suppressions nur mit einer aktuellen technischen Begründung behalten.
3. ContentDialogChrome neutral benennen.
4. Den sichtbaren Hinweis in alle Kanban-Lokalisierungsdateien aufnehmen.

## Testlücken – verpflichtende Reihenfolge

**Status: ✅ Vollständig geschlossen (2026-08-02)**

1. **Datenintegrität:** Instanzisolation des Default-Boards und Austausch aller Collections.
2. **Persistenzfehler:** Save-, Rename-, Delete- und Quota-/IO-Fehler in Desktop und Browser.
3. **Async-Lifecycle:** Provider-Wechsel, umgekehrte Completion-Reihenfolge, Unload und Exceptions.
4. **Reale Layoutgrenze:** Der aktuelle Collapse-Test in [KanbanBehaviorTests.cs](../Flowery.NET.Tests/KanbanBehaviorTests.cs#L39) baut einen isolierten Host mit einem künstlichen ItemTemplate. Er lädt nicht die vier echten FlowKanban-Layoutvarianten und hätte Unterschiede zwischen ihnen nicht gefunden. Standard, Compact, Swimlane-Header und Swimlane-Zelle müssen jeweils über das echte FlowKanban-Template gemessen werden.
5. **Resize plus Collapse:** Mindestens drei Spalten, eine kollabiert, danach Pointer- und Tastatur-Resize; sichtbare Breiten, belegter Platz und Gap-Positionen messen.
6. **Drag-and-drop:** Die reine CalculateInsertIndexFromLayout-Prüfung validiert die Mathematik, nicht die Zuordnung realisierter/recycelter Container. Einen Headless-Test bis zur realen Containerliste und einen UI-Smoke-Test für den vollständigen Ablauf behalten.
7. **Buttonmaße:** Nicht nur Sidebar und zwei Home-Buttons prüfen, sondern alle wiederverwendeten Toolbutton-Gruppen und Dialog-Footer gegen gemeinsame Messregeln.
8. **UAI-Semantik:** AutomationPeer-Patterns, HelpText, Werte und Sichtbarkeit testen; Name/AutomationId allein genügt nicht.
9. **Stresstest:** Die erzeugte Board-Datei mit mehreren hundert Karten laden und Suchfilter, Scrollen, Collapse, Resize, Drag-and-drop, Autosave und Wiederladen messen.

**Evidenz:** Instanzisolation sowie Columns-/Tasks-/Subtasks-Austausch sind durch Lifecycle-Regressionen abgedeckt. Save-, Rename-, Delete- und echte Desktop-IO-Fehler werden bis zur UI gemeldet; ein realer Browserlauf mit deaktiviertem `localStorage` prüft dieselben Mutationen über den produktiven WASM-Interop-Pfad. Kontrollierte Provider-Tests decken umgekehrte Completion-Reihenfolge, Unload und Exceptions ab. `When_SharedColumnSurfaceRealizes_AllLayoutsKeepMetricsAndGeometry` misst Standard, Compact, Swimlane-Header und Swimlane-Zellen am gemeinsamen echten Template; `When_CollapsedColumnIsResized_ByPointerAndKeyboard_GeometryRemainsConsistent` prüft Collapse, Pointer-/Tastatur-Resize und Gap-Geometrie. Der Drag-and-drop-Test arbeitet auf realisierten Containern. Kanban-Chrome, Home, Dialog-Footer und `DaisyIconText` verwenden gemeinsame Größen- und Zentrierungsprüfungen. Automationstests prüfen `IRangeValueProvider`, `IInvokeProvider`, `IExpandCollapseProvider`, Werte, HelpText und Sichtbarkeit. Der vorhandene Demoboard-Generator erzeugt im Stresstest 200 Karten; Suche, Scrollen, Collapse, Resize, Drag-and-drop, Autosave und Wiederladen bleiben innerhalb der gesetzten Grenzen. Die echte Gallery wurde mit gesetztem Avalonia-Telemetrie-Opt-out geprüft; Toolbar-, Spaltenkopf- und Statusbar-Inhalte sind sichtbar zentriert, die normalen Spaltenköpfe enthalten keine Lane-WIP-Überlagerungen, und die Dialog-/Menü-UAI-Kette ist semantisch ausführbar. Abschlussbelege: `review-dialog-final-tests.log` (`294/294`) und `review-dialog-final-build-all.log` (`10/10`, keine Warnungen oder Fehler).

## Positive Grundlagen, die beibehalten werden sollten

- JSON-Deserialisierung verwendet einen Source-Generation-Context.
- Der Board-Sanitizer setzt sinnvolle Obergrenzen für importierte Daten.
- Viele Template- und Provider-Subscriptions besitzen bereits explizite Detach-Pfade.
- Die Geometrieprüfungen für Buttonmaße decken die echten Standard-, Compact- und Swimlane-Templates sowie gemeinsame Icon-/Text-Inhalte ab.

## Urteil

✅ **Technisch commit-reif; bewusst nicht gestaged oder committet.**

Die priorisierten Blöcke und Testlücken sind vollständig geschlossen. Staging und Commit bleiben bis zur ausdrücklichen Freigabe des Benutzers ausgesetzt.

## Wichtigste Erkenntnis

Besitz-, Persistenz- und Lifecycle-Verträge sind jetzt zentral umgesetzt und über Fehlerpfade abgesichert. Die gemeinsame Spaltenoberfläche und das gemeinsame `DaisyIconText`-Template verhindern parallele Layoutkorrekturen in den vier Darstellungsvarianten.
