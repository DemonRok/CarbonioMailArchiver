# Changelog

Tutte le modifiche principali di Carbonio Mail Archiver sono documentate in questo file.

## [1.2.1] - In preparazione

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
