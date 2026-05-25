# Festival Controller Platform  
## Komplet Systembeskrivelse

---

# 1. Overordnet systembeskrivelse

Løsningen består af tre hoveddele:

1. **Festival Controller / Vagtstyring**
   - Hovedsystemet til koordinering, administration og overblik.

2. **Frivillig App**
   - Mobil/PWA-applikation til frivillige.

3. **Check-In App**
   - Hurtig QR-baseret check-in løsning til peakperioder.

Systemet er designet til festivaldrift, frivilligkoordinering, check-in, opgavestyring, GPS-overblik og kommunikation mellem frivillige og koordinatore.

---

# 2. Systemarkitektur og koncept

## Formål

Platformens formål er at:

- Skabe centralt overblik over frivillige
- Effektivisere check-in
- Understøtte koordinering på pladsen
- Sikre hurtig kommunikation
- Give live-status på poster og bemanding
- Dokumentere hændelser via billeder/video
- Skabe bedre overblik over ressourcer og behov

---

# 3. Applikationer i løsningen

## 3.1 Festival Controller (Hovedsystem)

Primært administrations- og koordinatorsystem.

### Brugertyper

Der findes to hovedroller:

- Administrator
- Koordinator

Frivillige logger **ikke** ind i dette system.

---

## 3.2 Frivillig App

Mobil/PWA-app til frivillige.

### Funktioner

- QR-check-in
- Vagtoverblik
- GPS-position
- Observationer
- Beskeder
- Profilstyring

---

## 3.3 Check-In App

Specialiseret check-in interface.

### Formål

- Hurtig scanning af frivillige
- Reducere køer ved indmøde
- Mobil check-in løsning til koordinatore

---

# 4. Login og adgangskontrol

## Login

Systemet kræver login for administratorer og koordinatore.

### Adgangsmodel

- Kun administratorer har adgang til administrationsmodulet.
- Koordinatore har adgang til driftsfunktioner.
- Frivillige anvender separat loginflow i frivilligappen.

---

# 5. Temaer og brugeroplevelse

Systemet understøtter:

- Lys tema
- Mørkt tema
- Systemtema (automatisk)

Brugerens præference gemmes individuelt.

---

# 6. Profilmodul

## Funktioner

Brugeren kan:

- Uploade profilbillede
- Ændre navn
- Ændre brugernavn
- Opdatere email
- Opdatere telefonnummer
- Ændre adgangskode
- Skifte tema

## Sikker redigering

Alle ændringer kræver aktiv bekræftelse.

Der findes:

- Gem
- Annuller

Så utilsigtede ændringer undgås.

---

# 7. Administrationsmodul

Kun tilgængeligt for administratorer.

Modulet er opdelt i tabs/sektioner.

---

# 8. Brugeradministration

## Funktioner

Administratorer kan:

- Oprette brugere
- Redigere brugere
- Slette brugere
- Spærre brugere
- Genåbne brugere

## Roller

Brugere kan tildeles roller som:

- Administrator
- Koordinator

---

# 9. Import/Export modul

## Formål

Automatisk import af frivillige og vagter fra CrewPlan.

## Funktioner

### Template download

Systemet kan generere en import-template.

### Direkte CrewPlan import

Importerede filer matches automatisk mod databasen.

---

## Delta-import logik

Systemet arbejder intelligent med opdateringer.

### Første import

- Alt oprettes.

### Efterfølgende importer

Kun ændringer opdateres.

### Eksempel

Hvis en frivillig allerede eksisterer:

- Eksisterende data slettes
- Nye data indsættes

Hvis en frivillig ikke længere findes i importfilen:

- Data beholdes i systemet

---

# 10. Statistik og tidsregistrering

## Eksport

Systemet kan eksportere:

- Timer
- Tidsforbrug
- Vagtaktivitet

Eksportformat:

- Excel

---

# 11. Statistikmodul

Detaljeret tidsregistrering.

## Indeholder

- Dato
- Start/slut tidspunkt
- Poster
- Flytninger mellem poster
- Total tid

## Understøtter

- Vagter over flere dage
- Nattevagter
- Historik

---

# 12. QR Scanner konfiguration

## Funktion

Valg af kamera til scanning.

Relevant ved:

- Flere kameraer
- Tablets
- Mobile check-in stationer

---

# 13. Kortsteder / Locations

Administrationsmodul til oprettelse af punkter på festivalpladsen.

## Typer

- Post
- Toilet
- Scene
- Førstehjælp
- Parkering
- Andet

## Funktioner

- Navn
- Beskrivelse
- Kategori
- Kortplacering
- GPS-koordinater

---

# 14. Kortmodul (Live map)

## Formål

Live-overblik over festivalområdet.

## Indeholder

- Kortsteder
- Frivillige
- GPS-positioner
- Filtrering

---

## GPS tracking

### PWA begrænsning

GPS opdateres kun når:

- Appen er åben
- Skærmen er aktiv

### Native app mulighed

Planlagt mulighed for:

- Ægte baggrundstracking
- Permanent GPS-opdatering

---

## Position status

Frivillige vises med statusfarver:

### Grøn

GPS-data under 1 time gammelt.

### Orange

GPS-data ældre end 1 time.

---

## Privacy og GDPR

GPS-data slettes automatisk når:

- Frivillig checker ud

Systemet tracker ikke frivillige uden aktiv check-in.

---

# 15. Beskedscenter

Kommunikationssystem mellem:

- Frivillige
- Koordinatore

---

## Funktioner

Frivillige kan indsende:

- Tekst
- Billeder
- Videoer
- Observationer

---

## Brugsscenarier

- Ødelagte faciliteter
- Slagsmål
- Hegnsovertrædelser
- Sikkerhedshændelser
- Generelle observationer

---

## Koordinatorfunktioner

Koordinatore kan:

- Besvare observationer
- Oprette opgaver
- Kommunikere retur

---

# 16. Opgavemodul

## Formål

Intern hændelses- og opgavestyring.

## Opgavestatus

- Åben
- I gang
- Udført

## Funktioner

- Titler
- Beskrivelser
- Noter
- Hændelseslog
- Historik

---

# 17. Frivilligmodul

Visualisering af importeret data.

## Indeholder

- Vagter
- Behov
- Tilmeldte
- Manglende frivillige

---

## Statusvisning

Eksempel:

- Behov: 3
- Tilmeldte: 4
- Mangler: 0

---

# 18. Overblik og kapacitetsvisualisering

Grafisk overblik over:

- Tilmeldte
- Behov
- Checkede ind

## Diagramtyper

- Bar charts
- Line charts

## Formål

At hjælpe koordinatore med:

- Ressourcestyring
- Over-/underbemanding
- Disponering af frivillige

---

# 19. Dashboard

Primært live-overbliksværktøj.

## Funktioner

- Poster
- Drag-and-drop
- Live check-in
- Søgning
- Alarmer
- Frivilligstatus

---

# 20. Poster og zoner

Dashboardet er opdelt i:

- Zoner
- Poster

Frivillige kan flyttes mellem poster via drag-and-drop.

---

# 21. Alarmfunktioner

## Individuelle alarmer

Poster kan have individuelle tidsgrænser.

Eksempel:

- Alarm efter 60 minutter

---

## Alarmhåndtering

Koordinatore kan:

- Slukke individuelle alarmer
- Slukke alle alarmer
- Nulstille alarmer

---

# 22. QR Check-In Flow

## Proces

1. Frivillig åbner QR-kode i app
2. Koordinator scanner kode
3. Frivillig checkes automatisk ind
4. Frivillig placeres i “Pitten”
5. Koordinator flytter frivillig til relevant post

---

# 23. Frivillig App – Loginflow

## Login metode

Passwordless login via emailkode.

## Flow

1. Email indtastes
2. Profil vælges
3. Engangskode sendes via mail
4. Brugeren logger ind

---

# 24. Frivillig App – Funktioner

## Profil

- QR-kode
- Tema
- Logout
- Kontaktoplysninger

---

## Overblik

Viser:

- Kommende vagter
- Countdown
- Aktiv status
- Aktuel post

---

## Vagter

Viser:

- Historik
- Status
- Udeblivelser
- Afviklede vagter
- Kommende vagter

---

## Timer

Frivillige kan se:

- Samlede timer
- Aktiv tid
- Historik

---

# 25. Frivillig aktivitetshistorik

Systemet logger:

- Check-in
- Postskift
- Check-out

Frivillige kan selv følge hele deres arbejdsdag.

---

# 26. Observationer fra frivillige

Frivillige kan:

- Tage billeder
- Optage video
- Skrive observationer

Alt sendes direkte til koordinatore.

---

# 27. Beskeder i frivilligappen

Chat-lignende interface.

## Funktioner

- Læst/ulæst
- Talebobler
- Direkte dialog
- Messenger-lignende oplevelse

---

# 28. Check-In App

## Formål

Hurtig indslusning af frivillige.

## Anvendelse

Kan bruges af:

- Koordinatore
- Eksterne hjælpere
- Midlertidigt personale

---

## Flow

1. Frivillig viser QR-kode
2. Check-In app scanner
3. Frivillig placeres automatisk i “Pitten”
4. Dashboard opdateres live
5. Frivillig får postinformation

---

# 29. Fremtidige funktioner (under udvikling)

## Rutevejledning

Planlagt integration:

- Kortnavigation
- Rutevejledning
- “Google Maps”-lignende navigation

### Formål

At hjælpe nye frivillige med at finde deres poster.

---

# 30. Teknologiske karakteristika

## Platformstyper

- Webapplikation
- PWA (Progressive Web App)

---

## Livefunktioner

- Realtidsopdateringer
- GPS tracking
- Live dashboard
- QR scanning

---

## Medieunderstøttelse

- Kamera
- Videooptagelse
- Billedupload

---

# 31. Styrker ved løsningen

## Drift

- Hurtigt overblik
- Effektiv koordinering
- Mindre kaos i peakperioder

## Frivillige

- Simpelt check-in
- Tydelige vagter
- Let kommunikation

## Koordinatore

- Live status
- Ressourceoverblik
- Hændelsesstyring
- Opgavestyring
