# Changelog

Tutte le modifiche principali di Carbonio Mail Archiver sono documentate in questo file.

## [1.7.5] - 2026-08-03

- Aggiunti retry sul download dei singoli EML in caso di timeout o errori HTTP temporanei.
- Aggiunto resume del download EML saltando i file gia' presenti e usando file temporanei `.download`.

## [1.7.4] - 2026-08-03

- Impostata sui file EML scaricati la data effettiva del messaggio quando disponibile.

## [1.7.3] - 2026-08-03

- Corretto il percorso locale del download EML salvando solo il contenuto della cartella selezionata.

## [1.7.2] - 2026-08-03

- Stabilizzate le metriche download EML e disabilitata la UI operativa durante le operazioni in corso.

## [1.7.1] - 2026-08-03

- Aggiunte metriche download EML con byte scaricati, velocita' media, tempo trascorso ed ETA.

## [1.7.0] - 2026-08-03

- Aggiunto download EML della cartella di destinazione o dell'Archivio, con ricostruzione dell'albero locale sotto la casella.
- Aggiunto limite opzionale di velocita' download in KB/s, con 0 come valore senza limiti.
- Aggiunta configurazione della cartella locale di download.
- Escluso il segmento `/Archive` dal percorso locale quando si scarica l'Archivio.
- Migliorata la gestione dell'annullamento download e della fase di conteggio iniziale.

## [1.6.1] - 2026-08-03

- Aggiunta conferma finale con pulsante OK al termine degli spostamenti completati.

## [1.6.0] - 2026-07-16

- Modificata la pulizia cartelle vuote: le cartelle vengono spostate nel cestino invece che eliminate.

## [1.5.0] - 2026-07-16

- Aggiunta eliminazione controllata delle cartelle sorgente/destinazione solo se vuote.

## [1.4.0] - 2026-07-16

- Aggiunta opzione per processare automaticamente sorgente e sottocartelle replicandole sotto Archivio.

## [1.3.0] - 2026-07-16

- Aggiunta destinazione automatica in Archivio con creazione del percorso `/Archive/...` coerente con la sorgente.

## [1.2.5] - 2026-07-16

- Corrette le date di rilascio nel changelog per le versioni gia' pubblicate.

## [1.2.4] - 2026-07-16

- Aggiunta indicazione visiva con barra indeterminata durante il caricamento cartelle.

## [1.2.3] - 2026-07-16

- Reso esplicito il salvataggio della selezione sorgente/destinazione nel JSON di configurazione.

## [1.2.2] - 2026-07-16

- Corretto il salvataggio di sorgente/destinazione dopo il caricamento cartelle.

## [1.2.1] - 2026-07-16

- Salvata e ripristinata l'ultima coppia di cartelle sorgente/destinazione selezionata.
- Migliorata la validazione dei campi numerici bloccando testo non numerico e incolla non valido.
- Rimossa dalla documentazione la creazione cartelle da app come attivita' prevista.

## [1.1.1] - 2026-07-16

- Impostata la dimensione iniziale finestra a 1118x844.
- Alzata la preview e aumentata leggermente l'area utile.
- Migliorata la descrizione delle opzioni nella tab Configurazione.
- Aggiunta gestione report CSV opzionale con cartella Reports e apertura ultimo report.
- Aggiornata la documentazione di progetto e introdotto il changelog.

## [1.1.0] - 2026-07-16

- Riorganizzata la UI con tab Connessione, Configurazione, Log e Info.
- Aggiunti controlli configurabili per preview, batch move e limite totale email.
- Impostato batch move configurabile tra 10 e 100.
- Aggiunta opzione esplicita per caricare le cartelle all'avvio.
- Spostato il logging diagnostico API/SOAP nella configurazione.
- Salvata automaticamente l'ultima data di ricerca e i parametri operativi nel JSON.
- Spostati i log nella cartella `Logs` accanto all'eseguibile.
- Migliorata la tab Info con versione, percorsi, licenza e link utili.

## [1.0.22] - 2026-07-16

- Aggiunta preview configurabile.
- Migliorato il layout della schermata principale e dello scroll.
- Aggiunta licenza MIT.

## [1.0.21] - 2026-07-16

- Introdotta versione centralizzata in `Directory.Build.props`.
- Aggiunta workflow GitHub Release.
- Mostrata la versione nel titolo finestra.

## Versioni precedenti

- Aggiunta base WPF/MVVM con DI.
- Aggiunto login Carbonio WebUI/API.
- Aggiunti caricamento cartelle, ricerca, preview email e nomi cartelle.
- Aggiunti spostamento preview, spostamento massivo, conteggio effettivo, progress bar e annullamento.
- Aggiunti report CSV, icona applicazione e publish Release senza PDB.
