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
- test ricerca in sola lettura con `SearchRequest`, preview a 10 messaggi;
- caricamento cartelle e selezione sorgente/destinazione in UI;
- caricamento automatico cartelle all'avvio dopo un primo caricamento riuscito;
- conteggio effettivo dei messaggi con ricerca paginata;
- spostamento reale della preview;
- spostamento reale dei risultati selezionati a batch, con default di 50 messaggi per chiamata e limite configurabile fino a 100;
- limite opzionale del numero di email da spostare (`0` = tutte);
- progress bar, annullamento cooperativo e log operazione;
- report CSV automatico in `%LocalAppData%\CarbonioMailArchiver\Reports`.

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
- `GetFolderRequest` per leggere ID, permessi e struttura cartelle.

Da verificare/implementare:

- `FolderActionRequest` o chiamata equivalente per creare cartelle da app;
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

Ogni spostamento batch genera un CSV in `%LocalAppData%\CarbonioMailArchiver\Reports` con:

- account;
- cartella sorgente e destinazione;
- data limite;
- batch size e limite richiesto;
- esito finale;
- riga per ogni messaggio selezionato, con stato `Spostato`, `Errore` o `Non spostato`.

La dimensione batch controlla quante email vengono inviate in una singola richiesta di spostamento. Il valore predefinito e' 50, il minimo configurabile e' 10 e il massimo configurabile e' 100. Il limite email e' separato: ad esempio, con limite 1001 e batch 50, l'app esegue 20 batch da 50 messaggi e un batch finale da 1 messaggio.

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

## Release GitHub

Il workflow `.github/workflows/release.yml` compila, esegue i test, pubblica la versione Release win-x64 self-contained senza PDB e genera uno ZIP.

Quando verra' creato un tag `v*`, il workflow aprira' una draft release GitHub con lo ZIP allegato. I tag e la pubblicazione finale della release restano manuali.

## Sicurezza

La password non viene salvata nel JSON di configurazione. Se l'utente abilita "Ricorda credenziali", la password viene protetta con DPAPI per l'utente Windows corrente. I certificati TLS non attendibili restano bloccati per impostazione predefinita.
