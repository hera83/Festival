# Festival Vagtstyring – Koncept- og Referencebeskrivelse

## 1. Formål

Dette dokument beskriver konceptet for version 3 af et system til styring af frivillige vagter på en festival.

Systemet skal bruges som et live operations- og dispatch-system, hvor administratorer kan:

- Importere frivillige og vagter fra et Excel-ark
- Checke frivillige ind og ud
- Flytte frivillige mellem “pitten” og aktive poster
- Overvåge hvor længe frivillige har været på poster
- Se alarmer og driftsstatus
- Finde personer hurtigt i dashboardet
- Se historik, statistik og eksportere data
- Koordinere med andre administratorer via en intern chat
- Administrere sæsoner/år, import, QR-koder, brugere og systemdata

Systemet er ikke primært et selvbetjeningssystem for frivillige. Frivillige skal ikke aktivt bruge systemet til drift. De møder op ved en skranke, hvorefter administratorer håndterer check-in, placering og check-out.

Frivillige kan dog have et simpelt informationsview, hvor de kan se deres egne vagter og relevante oplysninger.

---

## 2. Grundlæggende principper

### 2.1 Systemet er et live operations-board

Systemets vigtigste del er et live dashboard, der fungerer som et kontrolcenter for festivalens frivilligdrift.

Systemet skal ikke kun være en liste over personer og poster, men et værktøj der giver overblik over:

- Hvem er checket ind?
- Hvem er i pitten?
- Hvem står på hvilken post?
- Hvor længe har personer været på en post?
- Hvilke poster mangler bemanding?
- Hvilke personer skal snart flyttes eller afløses?
- Hvilke personer har været hvor og hvornår?
- Hvad sker der lige nu i driften?

### 2.2 Administratoren er operatøren

Frivillige er passive i driftsflowet.

Administratorer står for:

- Check-in
- Check-out
- Flytning mellem pitten og poster
- Alarmstyring
- QR-scanning
- Import
- Sæsonstyring
- Statistik og eksport

### 2.3 År/sæson er en hård afgrænser

Systemet arbejder med sæsoner/år.

Et festivalår er en central afgrænsning for næsten alle data.

Når et nyt år starter, skal alle operationelle data kunne nulstilles, men administratorer skal bevares.

Data fra tidligere år må ikke blandes ind i aktiv drift.

Tidligere års data må kun bruges til arkiv, historik og statistik.

### 2.4 Simpelt datagrundlag

Import og datamodel skal holdes så simpel som muligt.

De vigtigste importerede felter er:

- Key
- Navn
- Email
- Vagt dato
- Vagt start
- Vagt slut
- Vagt navn

Systemet skal ikke kræve unødvendige metadata for at fungere.

---

## 3. Centrale begreber

## 3.1 Sæson / år

En sæson repræsenterer et festivalår, eksempelvis 2026.

Sæsonen afgrænser:

- Frivillige
- Vagter
- QR-koder
- Check-ins
- Check-outs
- Pitten
- Poster
- Placeringer
- Eventlog
- Statistik
- Chatlog, hvis chatten er årsspecifik

Administratorer er ikke sæsonafhængige og skal bevares mellem år.

### Regler

- Aktiv drift må kun vise data fra aktiv sæson.
- Tidligere sæsoner må kun vises i arkiv/statistik.
- Frivillige fra tidligere år må ikke dukke op i check-in eller live dashboard.
- Ved nyt år skal systemet kunne starte med tomme sæsondata.

---

## 3.2 Administrator

En administrator er en permanent bruger i systemet.

Administratorer skal kunne:

- Logge ind
- Bruge live dashboard
- Checke frivillige ind og ud
- Flytte frivillige mellem pitten og poster
- Administrere poster
- Importere Excel-ark
- Administrere QR-koder
- Se statistik
- Eksportere data
- Administrere brugere og roller
- Bruge administratorchat
- Starte nyt år/sæson
- Slette sæsondata

Administratorer skal ikke slettes ved årsskifte.

---

## 3.3 Frivillig / bruger

En frivillig er en person, der er importeret via Excel-arket for en bestemt sæson.

Frivillige er ikke permanente på tværs af år.

En frivillig skal som minimum have:

- Key
- Navn
- Email
- QR-token
- QR-sendestatus
- Sæson/år

### Vigtige regler

- Frivillige er årsspecifikke.
- En frivillig fra 2025 er ikke samme aktive dataobjekt som en frivillig fra 2026.
- Key bruges til at matche frivillige inden for samme sæson/import.
- Frivillige må gerne checkes ind uden at have en planlagt vagt den pågældende dag.
- Check-in uden planlagt vagt skal stadig registreres i statistik og eventlog.

---

## 3.4 Key

Key er et nummer eller en unik reference fra et eksternt system.

Key er vigtig, fordi den gør det muligt at finde samme person mellem systemer.

### Regler for Key

- Key er den primære match-nøgle ved import.
- Hvis en Key allerede findes i aktiv sæson, skal importen erstatte den eksisterende bruger/vagtdata med data fra Excel-arket.
- Key bør ikke vises eller bruges direkte som QR-kode.
- QR-token skal være en separat intern token.

---

## 3.5 QR-token og QR-kode

QR-koden bruges til hurtigt check-in via en mobilvenlig administrator-side.

QR-koden er personlig for brugeren/frivilligen, men kun inden for den aktuelle sæson.

QR-koden genereres ved import og gemmes på brugeren.

### Regler

- QR-koden skal ikke sendes automatisk ved import.
- QR-token genereres automatisk ved import.
- Hvis en eksisterende Key importeres igen i samme sæson, skal QR-token genbruges.
- Hvis en bruger er ny i sæsonen, genereres en ny QR-token.
- QR-koden skal kunne sendes manuelt via administrationsmodulet.
- Systemet skal gemme om QR-koden er sendt.
- Systemet skal gemme hvornår QR-koden er sendt.
- Systemet bør gemme hvem der sendte QR-koden.

### Felter

- QrToken
- QrCodeSent
- QrCodeSentAt
- QrCodeSentBy

### QR-kode sendestatus

Administrator skal kunne se:

- QR ikke sendt
- QR sendt
- QR mangler email og kan derfor ikke sendes
- QR-afsendelse fejlede
- QR kan sendes igen

Ved gensendelse bør systemet advare:

> QR-koden er allerede sendt. Vil du sende den igen?

Hvis en brugers email ændres ved ny import, og QR-koden allerede er sendt, skal previewet vise en advarsel.

---

## 3.6 Vagt

En vagt er en planlagt frivilligvagt importeret fra Excel.

En frivillig kan kun have én planlagt vagt pr. dag.

### Felter

- Frivillig
- Dato
- Starttid
- Sluttid
- Vagt navn
- Sæson/år

### Regler

- En bruger må kun have én planlagt vagt pr. dag.
- Importen skal validere dette.
- Hvis samme Key har flere vagter samme dag, skal det vises som fejl eller tydelig advarsel.
- En bruger må godt checkes ind uden planlagt vagt.
- Ikke-planlagt check-in skal markeres som ekstra/manuelt check-in.

---

## 3.7 Pitten

Pitten er et midlertidigt område, hvor checkede personer opholder sig, indtil de sendes ud til en post.

Pitten fungerer som en ressource-pool.

Personer kommer typisk i pitten når:

- De checker ind
- De flyttes væk fra en post
- De holder pause
- De venter på ny placering

### Mulige statustyper i pitten

Systemet kan senere udvides med statusser som:

- Klar til udsendelse
- På pause
- Venter på opgave
- Skal snart hjem
- Har note
- Ikke planlagt vagt i dag

---

## 3.8 Post

En post er et fysisk eller organisatorisk område, hvor frivillige kan placeres.

Eksempler:

- Bar Nord
- Indgang
- Sceneområde
- Parkering
- Billetkontrol

En post fungerer som en container for frivillige.

### Post-egenskaber

En post bør kunne have:

- Navn
- Beskrivelse
- Sorteringsrækkefølge
- Aktiv/inaktiv status
- Alarmregel
- Minimeret/maksimeret visning
- Eventuelle bemandingskrav

### Alarmregel

En post kan have en regel som eksempelvis:

> Giv alarm når en person har været på posten i mere end 1 time.

Denne regel gælder alle brugere på posten, medmindre alarm er slået fra på den enkelte bruger.

### Brugerbaseret alarm override

På hver bruger skal man kunne deaktivere alarmen individuelt.

Eksempel:

- Posten har alarm efter 60 minutter
- Peter må gerne stå der længere
- Administrator slår alarm fra på Peter
- Peter bliver ikke markeret som overskredet, selvom andre gør

---

## 3.9 Eventlog

Eventloggen er en af de vigtigste dele af systemet.

Eventloggen er systemets historiske sandhed.

Den skal kunne bruges til:

- At se hvem der har været hvor
- At se hvornår personer blev checket ind
- At se hvornår personer blev flyttet
- At se hvornår personer blev checket ud
- At beregne statistik
- At eksportere data
- At debugge driften
- At dokumentere dagens forløb

### Eventtyper

Systemet bør som minimum registrere:

- Import gennemført
- Bruger oprettet via import
- Bruger erstattet via import
- QR-kode genereret
- QR-kode sendt
- QR-kode gensendt
- Check-in
- Check-in uden planlagt vagt
- Check-out
- Flyttet til pitten
- Flyttet fra pitten til post
- Flyttet fra post til post
- Alarm slået fra for bruger
- Alarm slået til for bruger
- Post oprettet
- Post ændret
- Post minimeret/maksimeret, hvis relevant
- Administratorhandlinger
- Sæson startet
- Sæsondata slettet
- Dummy data indlæst

### Eventlog bør gemme

- Tidspunkt
- Sæson
- Driftsdag/dato
- Handlingstype
- Berørt bruger/frivillig
- Fra-placering
- Til-placering
- Administrator
- Eventuel note
- Metadata

---

## 4. Roller og adgang

## 4.1 Administrator

Administratorer har adgang til alt.

Administratorer kan:

- Administrere hele systemet
- Importere Excel
- Se preview og gennemføre import
- Sende QR-koder
- Bruge live dashboard
- Checke personer ind og ud
- Flytte personer
- Administrere poster
- Se eventlog
- Se statistik
- Eksportere data
- Administrere brugere
- Starte nyt år
- Slette sæsondata
- Indlæse dummy data

## 4.2 Frivillig / bruger

Frivillige har kun et informationsview.

De skal kunne se:

- Deres egne vagter
- Hvilken dag de har vagt
- Hvilket vagt navn/vagttype de har
- Hvornår vagten starter og slutter
- Eventuelt aktuel status på dagen, hvis ønsket

Frivillige skal ikke kunne:

- Checke sig selv ind
- Checke sig selv ud
- Flytte sig selv
- Ændre data
- Se andre frivillige
- Se dashboard
- Se statistik
- Se adminchat

---

## 5. Live dashboard

Dashboardet er systemets primære arbejdsflade.

Det skal være optimeret til drift, overblik og hurtige handlinger.

Dashboardet bruges ofte af flere administratorer samtidig.

### 5.1 Layout

Tidligere versioner har haft 5 kolonner.

Version 3 kan stadig bygge videre på denne idé:

1. Administrationskolonne
2. Post-/dashboardkolonne 1
3. Post-/dashboardkolonne 2
4. Post-/dashboardkolonne 3
5. Post-/dashboardkolonne 4

Administrationskolonnen indeholder:

- Check-in-knap
- Søg/fokus-knap
- Pitten
- Checkede personer uden placering
- Maximer alle poster
- Minimer alle poster
- Eventuelt live aktivitet
- Eventuelt hurtigstatus

De øvrige kolonner indeholder poster.

### 5.2 Drag and drop

Alle relevante elementer skal kunne flyttes med drag and drop.

Det gælder:

- Frivillige mellem pitten og poster
- Frivillige mellem poster
- Eventuelt poster mellem kolonner
- Eventuelt sortering inden for poster

Drag and drop skal opdatere databasen og live dashboardet for alle administratorer.

### 5.3 Bruger-kort

Et bruger-kort i dashboardet bør vise:

- Navn
- Vagt navn
- Check-in tid
- Tid på nuværende placering
- Alarmstatus
- Om personen har planlagt vagt i dag
- Eventuelt ikon for QR/check-in/status
- Menu med handlinger

### 5.4 Bruger-menu

På bruger-elementet skal der være en lille menu.

Menuen skal indeholde:

- Check ud
- Flyt til pitten
- Flyt til post 1
- Flyt til post 2
- Flyt til post 3
- osv.
- Slå alarm fra/til for denne bruger

Menuen skal vise relevante poster.

“Flyt til pitten” skal ligge øverst.

### 5.5 Poster

Hver post skal være en container med brugere.

En post skal kunne:

- Vise brugere
- Vise antal personer
- Vise alarmstatus
- Minimeres
- Maksimeres
- Modtage drag and drop
- Have alarmregler
- Have brugerbaseret alarm override

### 5.6 Minimering og maksimering

Hver post skal have en knap til at minimere/maksimere posten.

Formålet er at gøre dashboardet mere overskueligt visuelt.

I første kolonne skal der være:

- Minimer alle
- Maksimer alle

Vigtig regel:

Live-opdateringer må ikke ændre administratorens lokale visningstilstand.

Hvis administrator B har minimeret Post 2, og administrator A flytter Peter til Post 2, skal Post 2 ikke automatisk åbnes hos administrator B.

### 5.7 Søgning og fokusvisning

Dashboardet skal have en søgefunktion til at finde en bruger blandt mange checkede personer.

Der kan være ca. 150 checkede brugere samtidig.

Søgningen skal kunne:

- Søge efter navn
- Søge efter key, hvis relevant for admin
- Finde brugeren i dashboardet
- Skjule alle andre brugere midlertidigt
- Fremhæve den fundne bruger
- Eventuelt vise brugerens aktuelle placering
- Kun søge i aktiv sæson

Fokusvisningen skal kunne ryddes igen.

Eksempel:

> Søg efter “Peter”
> Systemet skjuler alle andre personer
> Peter vises tydeligt på sin aktuelle post
> Administrator kan rydde fokus og se alle igen

---

## 6. Check-in flow

Check-in er et af de vigtigste flows i systemet.

Der skal understøttes både manuel check-in og QR-check-in.

## 6.1 Manuel check-in

Manuel check-in sker via check-in popup i dashboardet.

Flow:

1. Administrator åbner check-in popup
2. Administrator søger efter person
3. Systemet foreslår først personer med vagt i dag
4. Hvis administrator skriver videre, vises også personer i aktiv sæson uden vagt i dag
5. Administrator vælger person
6. Personen checkes ind
7. Personen placeres automatisk i pitten
8. Eventlog registrerer check-in
9. Dashboard opdateres live

### Søgelogik

Søgningen skal prioritere:

1. Personer med planlagt vagt i dag
2. Personer i aktiv sæson uden planlagt vagt i dag

Resultater fra tidligere år må aldrig vises.

Resultater kan grupperes visuelt:

- På vagt i dag
- Findes i sæsonen, men ikke på vagt i dag

Hvis personen ikke har planlagt vagt i dag, skal check-in stadig være muligt, men markeres som ekstra/manuelt check-in.

## 6.2 QR check-in

QR check-in sker via en mobilvenlig administrator-side.

Denne side er dedikeret til scanning af QR-koder.

Flow:

1. Administrator åbner mobil QR-scanner-side
2. Administrator scanner frivilligs QR-kode
3. Systemet finder brugeren i aktiv sæson
4. Systemet checker brugeren ind
5. Personen placeres automatisk i pitten
6. Systemet viser stor visuel bekræftelse
7. Eventlog registrerer QR-check-in
8. Dashboard opdateres live

### Vigtige regler

- QR-kode giver ikke frivilligen ret til selv at checke ind.
- QR-kode bruges af administrator som hurtig identifikation.
- QR-koden identificerer brugeren i aktiv sæson.
- Hvis QR-koden ikke findes i aktiv sæson, skal systemet vise fejl.
- Hvis brugeren allerede er checket ind, skal systemet vise tydelig status og undgå dobbelt check-in.

---

## 7. Check-out flow

Check-out kan ske fra:

- Brugerens menu i dashboardet
- Pitten
- En post
- Eventuelt en særskilt check-out dialog

Flow:

1. Administrator vælger check ud
2. Systemet registrerer check-out tidspunkt
3. Personen fjernes fra aktiv placering
4. Eventlog registrerer check-out
5. Statistik kan beregne samlet frivilligtid
6. Dashboard opdateres live

Hvis personen har været på flere poster, skal eventloggen kunne vise hele forløbet.

---

## 8. Importmodul

Importmodulet er centralt i systemet.

Import skal ske via XLSX Excel-ark.

Excel vælges fordi:

- Det er let at redigere manuelt
- Det passer godt til festivaldrift
- Det er lettere for ikke-tekniske administratorer
- Det minimerer problemer med separatorer
- Det kan fungere som masterplan
- Det giver bedre håndtering af dato og tid end CSV

## 8.1 Importformat

Systemet bør bruge en fast importskabelon.

Hovedarket kan hedde:

`Vagter`

Felter:

| Felt | Beskrivelse |
|---|---|
| Key | Unik reference fra eksternt system |
| Navn | Frivilligs navn |
| Email | Frivilligs email |
| Dato | Dato for vagten |
| Start | Starttidspunkt |
| Slut | Sluttidspunkt |
| VagtNavn | Navn/type på vagten |

## 8.2 Template-download

Administrationsmodulet bør have en funktion:

> Download importskabelon

Så administratorer altid kan få korrekt Excel-format.

## 8.3 Import preview

Importen må ikke gemmes direkte.

Der skal altid vises preview før import gennemføres.

Preview skal vise:

- Antal rækker læst
- Antal nye frivillige
- Antal eksisterende frivillige der erstattes
- Antal vagter der oprettes
- Antal gamle vagter der slettes
- Antal fejl
- Antal advarsler
- Hvilke brugere der oprettes
- Hvilke brugere der erstattes
- Hvilke brugere der har ændringer i navn/email
- Hvilke brugere der har sendt QR-kode og ændret email
- Om aktive/checkede brugere påvirkes

## 8.4 Importregel for eksisterende Key

Hvis en række i Excel-arket har en Key, som allerede findes i aktiv sæson:

> Systemet erstatter den eksisterende frivillige og alle tilhørende sæsondata med den nye version fra importen.

Det betyder:

- Eksisterende brugerdata opdateres/erstattes
- Gamle vagter for samme Key slettes
- Nye vagter fra Excel importeres
- QR-token genbruges
- QR-sendestatus bevares
- Eventuelle relevante advarsler vises i preview
- Eventuelle aktive check-ins/placeringer skal håndteres tydeligt

### Vigtig advarsel

Hvis en bruger, der erstattes, allerede er checket ind eller aktiv i dashboardet, skal previewet markere det tydeligt.

Eksempel:

> Denne bruger er aktiv/checket ind. Import vil påvirke aktiv placering og vagtdata.

Det bør kræve ekstra bekræftelse at importere ændringer, der påvirker aktive brugere.

## 8.5 QR-regler ved import

Ved import:

- Ny bruger får QR-token
- Eksisterende Key genbruger QR-token
- Nye brugere får `QrCodeSent = false`
- Eksisterende brugere bevarer `QrCodeSent`
- Eksisterende brugere bevarer `QrCodeSentAt`
- Eksisterende brugere bevarer `QrCodeSentBy`

QR-koder sendes ikke automatisk ved import.

## 8.6 Validering

Importen skal validere:

- Manglende Key
- Manglende navn
- Ugyldig email
- Manglende dato
- Manglende starttid
- Manglende sluttid
- Slut før start
- Samme Key har flere vagter samme dag
- Samme email bruges af flere keys
- Samme Key findes med forskellige navne i samme import
- Vagt uden vagt navn
- Rækker uden nødvendige data
- Dato uden for aktiv sæson, hvis relevant

## 8.7 Import-log

Systemet skal gemme importhistorik.

En import-log bør indeholde:

- Import-id
- Tidspunkt
- Administrator
- Filnavn
- Antal rækker
- Antal oprettede brugere
- Antal erstattede brugere
- Antal oprettede vagter
- Antal fejl/advarsler
- Importstatus
- Eventuelle detaljer

---

## 9. QR-administration

QR-administration skal ligge under administrationsmodulet.

Funktioner:

- Se alle brugere i aktiv sæson
- Filtrere på QR sendt/ikke sendt
- Filtrere på manglende email
- Sende QR til valgte brugere
- Gensende QR til valgte brugere
- Se hvornår QR sidst blev sendt
- Se hvem der sendte QR
- Se fejl ved afsendelse

### Vigtig regel

QR-koder må ikke sendes automatisk ved import.

Administrator skal aktivt vælge at sende dem.

---

## 10. Statistik og eventlog-view

Der skal være et særskilt view til eventlog, historik og statistik.

Dette view skal give overblik over alle relevante oplysninger.

## 10.1 Formål

Statistikmodulet skal kunne svare på:

- Hvem var hvor?
- Hvornår var de der?
- Hvor længe var de der?
- Hvor mange timer har en frivillig arbejdet?
- Hvor mange timer er brugt på en post?
- Hvem har været checket ind uden planlagt vagt?
- Hvem har været længst på samme post?
- Hvem blev checket ind og ud hvornår?
- Hvilke administratorer har udført hvilke handlinger?

## 10.2 Mulige visninger

- Personhistorik
- Posthistorik
- Dagsoversigt
- Sæsonoversigt
- Check-in/check-out historik
- Flyttehistorik
- Alarmhistorik
- Importhistorik
- Administratorhandlinger

## 10.3 Statistik pr. person

For hver person bør systemet kunne vise:

- Samlet frivilligtid
- Check-in tidspunkt
- Check-out tidspunkt
- Tid i pitten
- Tid på hver post
- Antal flytninger
- Eventuelle alarm overrides
- Om personen havde planlagt vagt
- Om personen var ekstra/manuelt checket ind

## 10.4 Statistik pr. post

For hver post bør systemet kunne vise:

- Samlet bemandingstid
- Antal personer der har været på posten
- Gennemsnitlig tid pr. person
- Længste tid på posten
- Perioder med lav bemanding
- Perioder med høj bemanding
- Alarmoverskridelser

## 10.5 Eksport

Systemet skal have eksportfunktion.

Eksportformater kan være:

- Excel/XLSX
- CSV

Eksport bør kunne filtreres på:

- Sæson
- Dato
- Person
- Post
- Vagt navn
- Eventtype
- Administrator

---

## 11. Realtid og samarbejde

Dashboardet bruges af flere administratorer samtidig.

Systemet skal derfor understøtte live-opdatering.

Hvis administrator A flytter Peter fra Post 1 til Post 2, skal administrator B kunne se ændringen uden at genindlæse siden.

### Vigtige regler

- Live-opdateringer skal opdatere data.
- Live-opdateringer må ikke ødelægge lokal visning.
- Hvis en post er minimeret hos en administrator, skal den forblive minimeret.
- Hvis en administrator har en søgning/fokusvisning aktiv, skal denne ikke automatisk ryddes.
- Lokale UI-præferencer må ikke overskrives af andre administratorers handlinger.

### Konflikter

Systemet bør håndtere konflikter.

Eksempel:

- Administrator A flytter Peter til Post 2
- Administrator B forsøger samtidig at flytte Peter til Post 3

Mulige løsninger:

- Sidste handling vinder
- Systemet viser advarsel
- Systemet registrerer begge forsøg i eventlog
- Administrator får besked om at personen allerede er flyttet

Anbefalet tilgang:

> Systemet skal altid gemme hvem der gjorde hvad og hvornår, så konflikter kan spores i eventloggen.

---

## 12. Administratorchat

Systemet skal have en lille administratorchat.

Chatten skal være en popup eller et let tilgængeligt panel.

Formålet er at administratorer kan koordinere, hvis de ikke sidder fysisk sammen.

### Funktioner

- Sende beskeder mellem administratorer
- Se beskeder live
- Se tidspunkt og afsender
- Eventuelt markere vigtige beskeder
- Eventuelt gemme chat i sæsonens historik

### Smart udvidelse

Chatten kan kombineres med systembeskeder.

Eksempler:

- Peter flyttet fra Pitten til Bar Nord af Admin A
- Anna har stået på Indgang i 75 minutter
- Post Bar Syd mangler bemanding
- QR-koder sendt til 25 brugere
- Import gennemført af Admin B

Dette gør chatten til en kombination af koordination og driftslog.

---

## 13. Sæsonstyring og nulstilling

Systemet skal have funktioner til at starte nyt år og slette data.

## 13.1 Start nyt år

Funktionen “Start nyt år” skal kunne:

- Oprette ny aktiv sæson
- Nulstille operationelle data
- Beholde administratorer
- Beholde systemindstillinger
- Gøre tidligere sæson til arkiv
- Gøre klar til ny import

Frivillige, vagter, QR-koder og eventlog fra tidligere år må ikke være aktive i ny sæson.

## 13.2 Slet alt

Der skal være en “slet alt”-funktion.

Denne funktion skal slette operationelle data, men ikke administratorer.

Den kan eksempelvis slette:

- Frivillige
- Vagter
- QR-koder
- Check-ins
- Placeringer
- Pitten
- Poster, hvis de er sæsonspecifikke
- Eventlog
- Statistik
- Chatlog
- Importhistorik

Administratorer skal bevares.

### Danger zone

Slet alt-funktioner skal ligge i et tydeligt markeret område.

Eksempel:

> Danger zone

Funktionen bør kræve ekstra bekræftelse.

Eksempel:

> Skriv SLET ALT for at fortsætte

## 13.3 Dummy data

Systemet skal kunne indlæse dummy data.

Dummy data bruges til:

- Test
- Træning af administratorer
- Demo
- Udvikling
- At lære systemet at kende før festivalen

Dummy data skal kunne fjernes igen.

---

## 14. Administrator-modul

Administrationsmodulet skal samle al systemopsætning.

### Funktioner

- Import af Excel
- Preview og validering
- Importhistorik
- QR-administration
- Brugeradministration
- Administratoradministration
- Rolleadministration
- Postadministration
- Sæsonstyring
- Dummy data
- Slet alt
- Statistik/eksport
- Systemindstillinger

---

## 15. Bruger-/frivillig-view

Frivillige skal have et simpelt informationsview.

Dette view er kun til information.

Det skal være pænt, overskueligt og mobilvenligt.

### Visning

Frivillig skal kunne se:

- Navn
- Vagt dato
- Vagt start
- Vagt slut
- Vagt navn
- Eventuelt QR-kode
- Eventuelt praktisk information
- Eventuelt aktuel status, hvis ønsket

Frivillige skal ikke have adgang til driftshandlinger.

---

## 16. UX og designprincipper

Systemet skal være hurtigt og nemt at bruge under pres.

Festivaldrift kan være hektisk, og administratorer skal kunne handle hurtigt.

### Designprincipper

- Simpelt og overskueligt
- Store tydelige knapper
- God kontrast
- Tydelige statusfarver
- Mobilvenlig QR-scanner
- Hurtig søgning
- Tydelige advarsler
- Minimal støj
- Dashboard skal kunne bruges på større skærme
- Admin-view skal være struktureret og roligt

### Statusfarver

Eksempel:

- Grøn: OK
- Gul: Nærmer sig alarm
- Rød: Alarm/overskredet
- Blå: I pitten
- Grå: Checket ud/inaktiv
- Orange: Ikke planlagt vagt

---

## 17. Alarmer

Poster kan have alarmregler.

Eksempel:

> Alarm efter 60 minutter på posten.

Alarmen gælder alle personer på posten, medmindre den deaktiveres på bruger-niveau.

### Alarmfunktioner

- Vise tid på post
- Markere personer der nærmer sig grænse
- Markere personer der har overskredet grænse
- Deaktivere alarm for enkeltperson
- Aktivere alarm igen
- Gemme alarm override i eventlog

---

## 18. Smart dispatch-forslag

Systemet kan senere udvides med smarte forslag.

Eksempel:

> Post Bar Nord mangler 2 personer. Disse personer i pitten passer bedst.

Forslag kan baseres på:

- Vagt navn
- Hvor længe personen har været i pitten
- Om personen har planlagt vagt i dag
- Om personen snart skal checkes ud
- Om personen tidligere har stået på posten
- Bemandingsbehov

Dette behøver ikke være AI i første version.

Det kan være almindelig forretningslogik.

---

## 19. Vigtige regler samlet

### Sæson

- Sæson/år er hård afgrænser.
- Kun aktiv sæson bruges i drift.
- Tidligere sæsoner er kun arkiv/statistik.
- Administratorer bevares mellem år.
- Frivillige bevares ikke som aktive data mellem år.

### Import

- Import sker via XLSX.
- Import må aldrig gemmes uden preview.
- Key er match-nøglen.
- Eksisterende Key i aktiv sæson erstattes med importdata.
- QR-token genbruges ved eksisterende Key.
- QR-sendestatus bevares ved eksisterende Key.
- QR sendes ikke automatisk.
- En bruger må kun have én planlagt vagt pr. dag.

### Check-in

- Check-in kan ske manuelt eller via QR.
- Check-in placerer personen i pitten.
- Check-in uden planlagt vagt er tilladt.
- Check-in uden planlagt vagt skal med i statistik.
- Søgepopup prioriterer dagens vagter, men kan også finde sæsonbrugere uden vagt i dag.
- Tidligere års brugere må ikke vises.

### Dashboard

- Dashboard er live.
- Flere administratorer kan arbejde samtidigt.
- Drag and drop skal opdatere live.
- Lokale filtre/minimeringer må ikke ændres af live-opdateringer.
- Søgning kan skjule alle andre brugere for at finde én person hurtigt.

### Eventlog

- Eventlog er systemets historiske sandhed.
- Alle vigtige handlinger skal logges.
- Statistik skal kunne beregnes ud fra eventlog.
- Eventlog skal kunne eksporteres.

---

## 20. Mulige fremtidige udvidelser

Følgende er ikke nødvendigvis krav i første version, men bør tænkes ind i konceptet:

- Smart dispatch-forslag
- Bemandingskrav pr. post
- Automatisk advarsel ved underbemanding
- Avanceret tidslinje pr. person
- Replay af dagens drift
- Offline fallback for QR-scanner
- SMS/email-integration
- Badge-print
- NFC-armbånd
- Avanceret rapportering
- AI-assistent til driftsforslag
- Rollebaseret begrænsning mellem administratorer
- Multi-festival support
- Dashboard på storskærm

---

## 21. Projektets kerneværdi

Version 3 skal ikke bare være en forbedret listevisning.

Den skal være et moderne live operations-system for festivalvagter.

Systemets vigtigste værdi er:

- Hurtigt check-in
- Let placering af frivillige
- Live overblik
- Historik over hvem der har været hvor
- Statistik over frivilligtimer
- Enkel import
- Sikker årshåndtering
- QR-understøttet check-in
- God drift for flere administratorer samtidig

---

## 22. Kort opsummering

Systemet består af følgende hovedmoduler:

1. Live Dashboard
2. Manuel check-in
3. Mobil QR check-in
4. Pitten
5. Poster
6. Drag and drop flytning
7. Alarmer
8. Eventlog
9. Statistik og eksport
10. Excel-import med preview
11. QR-administration
12. Sæsonstyring
13. Slet alt / dummy data
14. Administratorchat
15. Bruger-/frivillig informationsview
16. Bruger- og rolleadministration

Dette dokument skal bruges som reference gennem udviklingen, så systemet bygges så tæt på konceptet som muligt.