# Carbonio Mail Archiver

Applicazione desktop Windows in C# e WPF per analizzare e spostare in massa email presenti in una casella Carbonio usando API HTTP/SOAP lato server, senza IMAP, POP3 o EAS.

Licenza: MIT. Autore: Mauro Bettinelli.

## Stato

Base applicativa completata:

- solution e progetti separati;
- base WPF con pattern MVVM;
- Dependency Injection tramite `Microsoft.Extensions.Hosting`;
- configurazione utente in `%LocalAppData%\CarbonioMailArchiver\settings.json`;
- salvataggio password tramite DPAPI utente Windows;
- logging giornaliero nella cartella `Logs` accanto all'eseguibile;
- modelli e interfacce principali del dominio;
- test unitario iniziale per la costruzione query.

Funzioni operative disponibili:

- login reale tramite `POST /zx/auth/v2/login`;
- cookie di sessione `ZX_AUTH_TOKEN`/`ZM_AUTH_TOKEN` mantenuti solo in memoria;
- test connessione con `GetInfoRequest` JSON su `/service/soap/GetInfoRequest`;
- test ricerca in sola lettura con `SearchRequest`, preview configurabile fino a 100 messaggi;
- caricamento cartelle e selezione sorgente/destinazione in UI;
- destinazione automatica in Archivio, con creazione delle sottocartelle mancanti sotto `/Archive`;
- elaborazione opzionale della sorgente e di tutte le sue sottocartelle, una alla volta, in modalita' Archivio;
- eliminazione manuale controllata di cartelle sorgente/destinazione solo se vuote;
- caricamento automatico cartelle all'avvio, opzionale, se la password e' disponibile tramite DPAPI;
- conteggio effettivo dei messaggi con ricerca paginata;
- spostamento reale della preview;
- spostamento reale dei risultati selezionati a batch, con default di 50 messaggi per chiamata e limite configurabile fino a 100;
- limite opzionale del numero di email da spostare (`0` = tutte);
- progress bar, annullamento cooperativo e log operazione;
- report CSV opzionale al termine dello spostamento, salvato nella cartella `Reports` accanto all'eseguibile;
- configurazione dedicata con reset default e descrizione opzioni;
- tab Info con versione, percorsi, licenza e link utili;

## Struttura

```text
src/
  CarbonioMailArchiver.App
  CarbonioMailArchiver.Core
  CarbonioMailArchiver.Infrastructure
tests/
  CarbonioMailArchiver.Tests
```

## Compatibilita Carbonio

Gli endpoint SOAP possono variare tra installazioni Carbonio e tra provider. L'applicazione permette la configurazione manuale dell'URL SOAP e deve sempre eseguire un test di connessione prima degli spostamenti operativi.

Chiamate SOAP/API verificate o in uso:

- `POST /zx/auth/v2/login` con JSON `{ "auth_method": "password", "user": "...", "password": "..." }`, flusso usato dalla WebUI Carbonio;
- `GetInfoRequest` JSON su `/service/soap/GetInfoRequest`;
- `SearchRequest` diagnostica con query equivalente a `in:inbox before:dd/MM/yyyy`;
- `SearchRequest` su cartella scelta con query equivalente a `inid:<folderId> before:dd/MM/yyyy`;
- `MsgActionRequest` con azione `move` verso la cartella destinazione;
- `GetFolderRequest` per leggere ID, permessi e struttura cartelle;
- `CreateFolderRequest` per creare, solo in modalita' Archivio, i segmenti mancanti del percorso destinazione sotto `/Archive`.

Da valutare in futuro:

- report piu' ricco con metadati completi dei messaggi.

Informazioni reali utili dal server:

- URL pubblico esatto della WebUI e dell'endpoint SOAP;
- versione Carbonio e compatibilita delle API SOAP abilitate;
- eventuale 2FA, SSO, proxy, rate limit o restrizioni IP;
- comportamento TLS/certificato: i certificati non attendibili restano bloccati;
- permessi dell'utente per leggere cartelle e spostare messaggi;
- dimensione mailbox, volume stimato dei messaggi vecchi e limiti batch accettabili.

## Rischi tecnici

- le API SOAP Carbonio/Zimbra possono differire tra versioni o installazioni;
- l'operazione di move lato server e' distruttiva dal punto di vista della posizione corrente dei messaggi;
- il conteggio iniziale puo' cambiare durante l'esecuzione se arrivano o vengono spostate email;
- batch troppo grandi possono causare timeout, fault SOAP o limiti lato server;
- log diagnostici SOAP vanno filtrati per non registrare token o dati sensibili.

## Report operazione

Se l'opzione e' abilitata, al termine di uno spostamento l'app chiede se esportare un CSV nella cartella `Reports` accanto all'eseguibile con:

- account;
- cartella sorgente e destinazione;
- data limite;
- batch size e limite richiesto;
- esito finale;
- riga per ogni messaggio selezionato, con stato `Spostato`, `Errore` o `Non spostato`.

La dimensione batch controlla quante email vengono inviate in una singola richiesta di spostamento. Il valore predefinito e' 50, il minimo configurabile e' 10 e il massimo configurabile e' 100. Il limite email e' separato: ad esempio, con limite 1001 e batch 50, l'app esegue 20 batch da 50 messaggi e un batch finale da 1 messaggio.

## Modalita Archivio

Quando l'opzione Archivio e' attiva, la selezione manuale della destinazione viene disabilitata. L'app calcola la destinazione replicando il percorso sorgente sotto `/Archive`.

Esempi:

- sorgente `/Inbox/ANIMALI_UDA` -> destinazione `/Archive/Inbox/ANIMALI_UDA`;
- sorgente `/Inbox/ANIMALI_UDA/Esempio` -> destinazione `/Archive/Inbox/ANIMALI_UDA/Esempio`.

Prima dello spostamento reale l'app verifica se il percorso esiste; se mancano cartelle intermedie, le crea una alla volta e poi sposta i messaggi nella cartella finale.

Se l'opzione "Includi sottocartelle" e' attiva, l'app processa la cartella sorgente selezionata e poi ogni sottocartella, in ordine di percorso. Per ogni cartella viene calcolata e creata, se necessario, la destinazione corrispondente sotto `/Archive`. L'opzione richiede la modalita' Archivio per evitare spostamenti massivi verso una singola destinazione manuale.

## Eliminazione cartelle vuote

I pulsanti "Elimina vuote" su sorgente e destinazione ricaricano lo stato dal server prima di eseguire la cancellazione. Se "Includi sottocartelle" non e' attivo, viene valutata solo la cartella selezionata. Se "Includi sottocartelle" e' attivo, l'app valuta il ramo selezionato in modo ricorsivo ed elimina dal livello piu' profondo verso l'alto. L'app non elimina cartelle di sistema, cartelle non modificabili, cartelle con messaggi o rami che contengono cartelle non vuote.

## Build

```bat
dotnet build CarbonioMailArchiver.slnx
```

## Versione

La versione corrente e' centralizzata in `Directory.Build.props`, proprieta' `BuildVersion`.

Versione iniziale: `1.0.21`.

Prima di ogni commit destinato a una nuova build, aggiornare `BuildVersion`. La stessa versione viene applicata agli assembly e mostrata nel titolo della finestra.

## Test

```bat
dotnet test CarbonioMailArchiver.slnx
```

## Pubblicazione prevista

```bat
dotnet publish src\CarbonioMailArchiver.App\CarbonioMailArchiver.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false -o artifacts\release\CarbonioMailArchiver
```

La build Release non genera PDB. L'eseguibile pubblicato si trova in `artifacts\release\CarbonioMailArchiver\CarbonioMailArchiver.App.exe`.

## Changelog

Le modifiche principali sono tracciate in `CHANGELOG.md`. Ogni release deve aggiornare `Directory.Build.props` e aggiungere una voce al changelog prima del tag.

## Release GitHub

Il workflow `.github/workflows/release.yml` compila, esegue i test, pubblica la versione Release win-x64 self-contained senza PDB e genera uno ZIP.

Quando viene creato un tag `v*`, il workflow crea una release GitHub pubblica con lo ZIP allegato.

## Sicurezza

La password non viene salvata nel JSON di configurazione. Se l'utente abilita "Ricorda credenziali", la password viene protetta con DPAPI per l'utente Windows corrente. I certificati TLS non attendibili restano bloccati per impostazione predefinita.
