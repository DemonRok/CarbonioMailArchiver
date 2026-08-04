# Changelog

Tutte le modifiche principali di Carbonio Mail Archiver sono documentate in questo file.

## [1.8.19] - 2026-08-04

- Allineata la versione corrente a `1.8.19` nei metadati di build.
- Ripulita la documentazione principale per renderla più coerente e meno verbosa.

## [1.8.17] - 2026-08-04

- Sostituita la conferma modale sulla cancellazione delle cartelle vuote con una finestra dedicata non bloccante, così la preview resta consultabile.

## [1.8.7] - 2026-08-04

- Ripristinato il messaggio di stato operativo al posto dell'aggiornamento tecnico del log.
- Rigenerata l'icona dell'app a partire dal PNG trasparente aggiornato.

## [1.8.6] - 2026-08-04

- Reso il pulsante `Comprimi EML` disponibile solo dopo una verifica dei download completata con esito positivo.

## [1.8.5] - 2026-08-04

- Aggiunto filtro per livello nel Log e intestazioni cliccabili per ordinare le colonne.
- Reso il Log leggibile con vista tabellare e righe più facili da scandire.

## [1.8.4] - 2026-08-04

- Resa piu' leggibile la scheda Log con righe compatte, sorgente abbreviata e messaggi formattati in modo umano.
- Mantena la copia completa del log grezzo per il debug e gli appunti.

## [1.8.3] - 2026-08-04

- Verificata e predisposta la creazione integrata di archivi `.7z` senza eseguibile esterno di 7-Zip.
- Aggiunta configurazione del livello di compressione 7z con profili Veloce, Normale, Bilanciata e Massima.
- Aggiunto pulsante `Comprimi EML`: verifica i download EML, crea l'archivio `.7z` della casella e rimuove la cartella non compressa solo a compressione riuscita.
- Aggiunto pulsante `Apri Download` nella scheda Info e tradotti in italiano i pulsanti della scheda.

## [1.8.2] - 2026-08-04

- Aggiunto download EML con progressi, ETA, velocita' e controlli di annullamento più robusti.
- Introdotti resume e retry sul download EML con ripartenza dei file incompleti.
- Aggiunta verifica EML dei file gia' presenti e correzione dei timestamp sui messaggi scaricati.

## [1.8.1] - 2026-08-04

- Aggiunta scheda Configurazione per le opzioni operative e la persistenza della cartella download EML.
- Aggiunta selezione della cartella download EML tramite finestra cartelle di Windows.
- Aggiunta persistenza di sorgente, destinazione e data di ricerca.

## [1.7.8] - 2026-08-03

- Spostata su thread di background la verifica/riparazione timestamp EML per mantenere reattiva la finestra.

## [1.7.7] - 2026-08-03

- Aggiunta verifica EML automatica a fine download, errore o annullamento, con conteggio presenti/mancanti.
- Aggiunto pulsante `Verifica EML` per confrontare i messaggi attesi dal server con i file EML gia' presenti su disco.
- Aggiornata la data dei file EML gia' presenti durante resume e verifica, usando la data effettiva del messaggio.

## [1.7.6] - 2026-08-03

- Corretto il rilascio del file temporaneo EML prima del rinomina finale.
- Migliorato il calcolo ETA del download EML con resume: la stima usa solo le email scaricate nella sessione corrente.
- Separate le metriche operative di download in righe dedicate per trascorso, velocita', MB scaricati ed ETA.
- Separati contatore, cartella corrente e file corrente durante il download EML per ridurre oscillazioni del testo.
- Ridotto a 5 secondi l'intervallo di ricalcolo ETA dopo la prima stima stabile.

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
