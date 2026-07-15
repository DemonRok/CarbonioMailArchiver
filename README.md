# Carbonio Mail Archiver

Applicazione desktop Windows in C# e WPF per analizzare e spostare in massa email presenti in una casella Carbonio usando API HTTP/SOAP lato server, senza IMAP, POP3 o EAS.

## Stato

Fase A completata:

- solution e progetti separati;
- base WPF con pattern MVVM;
- Dependency Injection tramite `Microsoft.Extensions.Hosting`;
- configurazione utente in `%LocalAppData%\CarbonioMailArchiver\settings.json`;
- salvataggio password tramite DPAPI utente Windows;
- logging giornaliero in `%LocalAppData%\CarbonioMailArchiver\Logs`;
- modelli e interfacce principali del dominio;
- test unitario iniziale per la costruzione query.

Fase B diagnostica avviata:

- login reale tramite `POST /zx/auth/v2/login`;
- cookie di sessione `ZX_AUTH_TOKEN`/`ZM_AUTH_TOKEN` mantenuti solo in memoria;
- test connessione con `GetInfoRequest` JSON su `/service/soap/GetInfoRequest`;
- test ricerca in sola lettura con `SearchRequest`, limite 10 messaggi;
- pulsanti UI "Test connessione" e "Test ricerca";
- nessuno spostamento email ancora implementato.

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

Gli endpoint SOAP possono variare tra installazioni Carbonio e tra provider. L'applicazione deve sempre eseguire prima il test di connessione e deve permettere la configurazione manuale dell'URL SOAP. L'abilitazione della voce CLI nella Admin UI non implica accesso SSH o disponibilita di comandi server-side.

Chiamate SOAP/API verificate o da verificare in Fase B:

- `POST /zx/auth/v2/login` con JSON `{ "auth_method": "password", "user": "...", "password": "..." }`, flusso usato dalla WebUI Carbonio;
- `GetInfoRequest` JSON su `/service/soap/GetInfoRequest`;
- `SearchRequest` diagnostica con query equivalente a `in:inbox before:yyyy/MM/dd`;
- `FolderActionRequest` o chiamata equivalente per creare la cartella archivio sotto Inbox;
- `MsgActionRequest` con azione `move` verso la cartella destinazione;
- `GetFolderRequest` per leggere ID, permessi e struttura cartelle.

Endpoint da verificare nelle fasi successive:

- `https://host/service/soap`;
- endpoint equivalenti usati dalla WebUI Carbonio;
- eventuali path diversi pubblicati da reverse proxy o tenant.

Informazioni reali necessarie dal server prima della Fase B:

- URL pubblico esatto della WebUI e dell'endpoint SOAP;
- versione Carbonio e compatibilita delle API SOAP abilitate;
- formato account richiesto per login, dominio e tenant;
- eventuale 2FA, SSO, proxy, rate limit o restrizioni IP;
- comportamento TLS/certificato: i certificati non attendibili restano bloccati;
- permessi dell'utente per leggere Inbox, creare sottocartelle e spostare messaggi;
- dimensione mailbox, volume stimato dei messaggi vecchi e limiti batch accettabili.

## Rischi tecnici

- le API SOAP Carbonio/Zimbra possono differire tra versioni o installazioni;
- l'operazione di move lato server e' distruttiva dal punto di vista della posizione corrente dei messaggi;
- il conteggio iniziale puo' cambiare durante l'esecuzione se arrivano o vengono spostate email;
- batch troppo grandi possono causare timeout, fault SOAP o limiti lato server;
- log diagnostici SOAP vanno filtrati per non registrare token o dati sensibili.

## Fase B non inclusa

La diagnostica Fase B implementa login, `NoOpRequest` e `SearchRequest` in sola lettura con limite basso. Non implementa ancora creazione cartelle, spostamento messaggi o export CSV operativo. I servizi relativi restano registrati come placeholder espliciti.

## Build

```bat
dotnet build CarbonioMailArchiver.slnx
```

## Test

```bat
dotnet test CarbonioMailArchiver.slnx
```

## Pubblicazione prevista

```bat
dotnet publish src\CarbonioMailArchiver.App\CarbonioMailArchiver.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Sicurezza

La password non viene salvata nel JSON di configurazione. Se l'utente abilita "Ricorda credenziali", la password viene protetta con DPAPI per l'utente Windows corrente. I certificati TLS non attendibili restano bloccati per impostazione predefinita.
