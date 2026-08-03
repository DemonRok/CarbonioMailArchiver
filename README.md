# Carbonio Mail Archiver

Applicazione desktop Windows in C# e WPF per analizzare e spostare in massa email presenti in una casella Carbonio usando API HTTP/SOAP lato server, senza IMAP, POP3 o EAS.

Licenza: MIT. Autore: Mauro Bettinelli.

## Funzioni

- login reale tramite `POST /zx/auth/v2/login`;
- cookie di sessione `ZX_AUTH_TOKEN`/`ZM_AUTH_TOKEN` mantenuti solo in memoria;
- test connessione con `GetInfoRequest` JSON su `/service/soap/GetInfoRequest`;
- test ricerca in sola lettura con `SearchRequest`, preview configurabile fino a 100 messaggi;
- caricamento cartelle e selezione sorgente/destinazione in UI;
- destinazione automatica in Archivio, con creazione delle sottocartelle mancanti sotto `/Archive`;
- elaborazione opzionale della sorgente e di tutte le sue sottocartelle, una alla volta, in modalita' Archivio;
- spostamento manuale controllato nel Cestino di cartelle sorgente/destinazione solo se vuote;
- caricamento automatico cartelle all'avvio, opzionale, se la password e' disponibile tramite DPAPI;
- conteggio effettivo dei messaggi con ricerca paginata;
- spostamento reale della preview;
- spostamento reale dei risultati selezionati a batch, con default di 50 messaggi per chiamata e limite configurabile fino a 100;
- limite opzionale del numero di email da spostare (`0` = tutte);
- progress bar, annullamento cooperativo e log operazione;
- report CSV opzionale al termine dello spostamento, salvato nella cartella `Reports` accanto all'eseguibile;
- download EML della destinazione selezionata o dell'Archivio, con ricostruzione dell'albero cartelle sotto la casella;
- limite opzionale di velocita' download in KB/s (`0` = senza limite);
- configurazione dedicata con reset default e descrizione opzioni;
- tab Info con versione, percorsi, licenza e link utili;

## Compatibilita Carbonio

Gli endpoint SOAP possono variare tra installazioni Carbonio e tra provider. L'applicazione permette la configurazione manuale dell'URL SOAP ed e' consigliato eseguire un test di connessione prima degli spostamenti operativi.

Chiamate SOAP/API verificate o in uso:

- `POST /zx/auth/v2/login` con JSON `{ "auth_method": "password", "user": "...", "password": "..." }`, flusso usato dalla WebUI Carbonio;
- `GetInfoRequest` JSON su `/service/soap/GetInfoRequest`;
- `SearchRequest` diagnostica con query equivalente a `in:inbox before:dd/MM/yyyy`;
- `SearchRequest` su cartella scelta con query equivalente a `inid:<folderId> before:dd/MM/yyyy`;
- `MsgActionRequest` con azione `move` verso la cartella destinazione;
- `GetFolderRequest` per leggere ID, permessi e struttura cartelle;
- `CreateFolderRequest` per creare, solo in modalita' Archivio, i segmenti mancanti del percorso destinazione sotto `/Archive`;
- download raw EML tramite endpoint home Carbonio/Zimbra con `fmt=raw`.

## Report operazione

Se l'opzione e' abilitata, al termine di uno spostamento l'app chiede se esportare un CSV nella cartella `Reports` accanto all'eseguibile con:

- account;
- cartella sorgente e destinazione;
- data limite;
- batch size e limite richiesto;
- esito finale;
- riga per ogni messaggio selezionato, con stato `Spostato`, `Errore` o `Non spostato`.

La dimensione batch controlla quante email vengono inviate in una singola richiesta di spostamento. Il valore predefinito e' 50, il minimo configurabile e' 10 e il massimo configurabile e' 100. Il limite email e' separato: ad esempio, con limite 1001 e batch 50, l'app esegue 20 batch da 50 messaggi e un batch finale da 1 messaggio.

## Download EML

Il pulsante `Scarica EML` nella schermata principale scarica tutti gli `.eml` della cartella scelta e delle sue sottocartelle.

- Se Archivio e' attivo, la radice del download e' `/Archive`.
- Se Archivio non e' attivo, la radice del download e' la cartella di destinazione selezionata.
- La cartella locale di base e' configurabile; sotto questa viene creata una directory con il nome della casella e poi il contenuto della radice scaricata, ad esempio `Downloads\utente@example.test\Inbox\APC` per `/Archive/Inbox/APC`.
- Il limite velocita' e' espresso in KB/s; `0` significa nessun limite applicato dall'app.

## Modalita Archivio

Quando l'opzione Archivio e' attiva, la selezione manuale della destinazione viene disabilitata. L'app calcola la destinazione replicando il percorso sorgente sotto `/Archive`.

Esempi:

- sorgente `/Inbox/ANIMALI_UDA` -> destinazione `/Archive/Inbox/ANIMALI_UDA`;
- sorgente `/Inbox/ANIMALI_UDA/Esempio` -> destinazione `/Archive/Inbox/ANIMALI_UDA/Esempio`.

Prima dello spostamento reale l'app verifica se il percorso esiste; se mancano cartelle intermedie, le crea una alla volta e poi sposta i messaggi nella cartella finale.

Se l'opzione "Includi sottocartelle" e' attiva, l'app processa la cartella sorgente selezionata e poi ogni sottocartella, in ordine di percorso. Per ogni cartella viene calcolata e creata, se necessario, la destinazione corrispondente sotto `/Archive`. L'opzione richiede la modalita' Archivio per evitare spostamenti massivi verso una singola destinazione manuale.

## Cestino cartelle vuote

I pulsanti "Cestina vuote" su sorgente e destinazione ricaricano lo stato dal server prima di spostare cartelle nel Cestino. Se "Includi sottocartelle" non e' attivo, viene valutata solo la cartella selezionata. Se "Includi sottocartelle" e' attivo, l'app valuta il ramo selezionato in modo ricorsivo e sposta nel Cestino dal livello piu' profondo verso l'alto. L'app non sposta cartelle di sistema, cartelle non modificabili, cartelle con messaggi o rami che contengono cartelle non vuote.

## Build

```bat
dotnet build CarbonioMailArchiver.slnx
```

## Versione

La versione corrente e' centralizzata in `Directory.Build.props`, proprieta' `BuildVersion`.
La stessa versione viene applicata agli assembly e mostrata nel titolo della finestra.

## Test

```bat
dotnet test CarbonioMailArchiver.slnx
```

## Pubblicazione locale

```bat
dotnet publish src\CarbonioMailArchiver.App\CarbonioMailArchiver.App.csproj -c Release -r win-x64 --self-contained false -o publish\win-x64
```

La build Release non genera PDB. L'eseguibile pubblicato si trova in `publish\win-x64\CarbonioMailArchiver.App.exe`.

## Changelog

Le modifiche principali sono tracciate in `CHANGELOG.md`.

## Release GitHub

Il workflow `.github/workflows/release.yml` compila, esegue i test, pubblica la versione Release win-x64 self-contained senza PDB e genera uno ZIP.

Quando viene creato un tag `v*`, il workflow crea una release GitHub pubblica con lo ZIP allegato.

## Sicurezza

La password non viene salvata nel JSON di configurazione. Se l'utente abilita "Ricorda credenziali", la password viene protetta con DPAPI per l'utente Windows corrente. I certificati TLS non attendibili restano bloccati per impostazione predefinita.
