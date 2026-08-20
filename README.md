# Carbonio Mail Archiver

Applicazione desktop Windows per analizzare e spostare in massa email presenti in una casella Carbonio, senza usare IMAP, POP3 o EAS.

Licenza: MIT. Autore: Mauro Bettinelli.

## Funzioni

- test di connessione e ricerca in sola lettura;
- preview configurabile fino a 100 messaggi;
- caricamento cartelle e selezione sorgente/destinazione in UI;
- esclusione predefinita delle cartelle speciali, con opzione per visualizzarle;
- destinazione automatica in Archivio, con creazione delle sottocartelle mancanti sotto `/Archive`;
- elaborazione opzionale della sorgente e di tutte le sue sottocartelle, una alla volta, in modalita' Archivio;
- spostamento manuale controllato nel Cestino di cartelle sorgente/destinazione solo se vuote;
- caricamento automatico cartelle all'avvio, opzionale, quando la password e' disponibile tramite DPAPI;
- conteggio effettivo dei messaggi prima dello spostamento;
- spostamento reale della preview;
- spostamento reale dei risultati selezionati a batch, con default di 250 messaggi per chiamata e limite configurabile fino a 500;
- limite opzionale del numero di email da spostare (`0` = tutte);
- progress bar, annullamento cooperativo e log operazione;
- report CSV opzionale al termine dello spostamento, salvato nella cartella `Reports` accanto all'eseguibile;
- download EML della destinazione selezionata o dell'Archivio, con ricostruzione dell'albero cartelle sotto la casella;
- persistenza della cartella da scaricare selezionata: l'opzione Archivio mostra `/Archive` senza perdere la selezione manuale da ripristinare quando viene disattivata;
- limite opzionale di velocita' download in KB/s (`0` = senza limite);
- retry configurabili per i download falliti, con ritardo progressivo;
- verifica dei file EML scaricati e compressione 7z disponibile dopo una verifica riuscita;
- spostamento nel Cestino delle email scaricate dopo una verifica EML riuscita, con conferma;
- configurazione dedicata con reset default e descrizione chiara delle opzioni;
- tab Info con versione, percorsi, licenza e link utili;
- riduzione configurabile nella traybar di Windows, con ripristino dal doppio clic o dal menu dell'icona;

## Avvio rapido

1. Estrai il contenuto dello ZIP in una cartella locale.
2. Avvia `CarbonioMailArchiver.App.exe`.
3. Inserisci URL Carbonio, account e password nella scheda `Connessione`.
4. Premi `Salva`, poi `Carica cartelle` se il caricamento automatico non e' attivo.
5. Seleziona sorgente e destinazione, esegui un `Test ricerca` e controlla la preview.

Per operazioni importanti e' consigliato effettuare prima una prova con `Sposta preview`.

## Report operazione

Se l'opzione e' abilitata, al termine di uno spostamento l'app chiede se esportare un CSV nella cartella `Reports` accanto all'eseguibile con:

- account;
- cartella sorgente e destinazione;
- data limite;
- batch size e limite richiesto;
- esito finale;
- riga per ogni messaggio selezionato, con stato `Spostato`, `Errore` o `Non spostato`.

La dimensione batch controlla quante email vengono inviate in una singola richiesta di spostamento. Il valore predefinito e' 250, il minimo configurabile e' 10 e il massimo configurabile e' 500. Il limite email e' separato: ad esempio, con limite 1001 e batch 250, l'app esegue 4 batch da 250 messaggi e un batch finale da 1 messaggio.

## Download EML

Il pulsante `Scarica EML` nella schermata principale scarica tutti gli `.eml` della cartella scelta e delle sue sottocartelle. In modalita' normale rispetta il campo `Cerca prima del`; con `Scarica tutta la casella ignorando la data` parte dalla radice `/` e ricrea l'intero albero delle cartelle.

- Se Archivio e' attivo, la radice del download e' `/Archive`.
- Se Archivio non e' attivo, la radice del download e' la cartella di destinazione selezionata.
- La cartella locale di base e' configurabile; sotto questa viene creata una directory con il nome della casella e poi il contenuto della radice scaricata, ad esempio `Downloads\utente@example.test\Inbox\APC` per `/Archive/Inbox/APC`.
- Il limite velocita' e' espresso in KB/s; `0` significa nessun limite applicato dall'app.
- I file gia' presenti vengono saltati per consentire il resume; la verifica EML confronta i messaggi attesi con i file locali prima di abilitare la compressione.
- Dopo una verifica riuscita e' possibile spostare nel Cestino le email verificate; i file EML locali non vengono cancellati. La struttura delle cartelle sorgente viene ricreata sotto `/Trash`, ad esempio `/Inbox/Animali/Esempio` diventa `/Trash/Inbox/Animali/Esempio`.

## Modalita Archivio

Quando l'opzione Archivio e' attiva, la selezione manuale della destinazione viene disabilitata. L'app calcola la destinazione replicando il percorso sorgente sotto `/Archive`.

Esempi:

- sorgente `/Inbox/ANIMALI_UDA` -> destinazione `/Archive/Inbox/ANIMALI_UDA`;
- sorgente `/Inbox/ANIMALI_UDA/Esempio` -> destinazione `/Archive/Inbox/ANIMALI_UDA/Esempio`.

Prima dello spostamento reale l'app verifica se il percorso esiste; se mancano cartelle intermedie, le crea una alla volta e poi sposta i messaggi nella cartella finale.

Se l'opzione "Includi sottocartelle" e' attiva, l'app processa la cartella sorgente selezionata e poi ogni sottocartella, in ordine di percorso. Per ogni cartella viene calcolata e creata, se necessario, la destinazione corrispondente sotto `/Archive`. L'opzione richiede la modalita' Archivio per evitare spostamenti massivi verso una singola destinazione manuale.

## Cestino cartelle vuote

I pulsanti "Cestina vuote" su sorgente e destinazione ricaricano lo stato dal server prima di spostare cartelle nel Cestino. Se "Includi sottocartelle" non e' attivo, viene valutata solo la cartella selezionata. Se "Includi sottocartelle" e' attivo, l'app valuta il ramo selezionato in modo ricorsivo e sposta nel Cestino dal livello piu' profondo verso l'alto. L'app non sposta cartelle di sistema, cartelle non modificabili, cartelle con messaggi o rami che contengono cartelle non vuote.

## Sicurezza

La password non viene salvata nel JSON di configurazione. Se l'utente abilita "Ricorda credenziali", la password viene protetta con DPAPI per l'utente Windows corrente. I certificati TLS non attendibili restano bloccati per impostazione predefinita.
