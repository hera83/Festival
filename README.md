# Festival Vagtstyring

Festival Vagtstyring er et ASP.NET Core websystem til live drift af frivillige på en festival. Systemet bruges til at planlægge vagter, checke frivillige ind/ud, flytte dem mellem pit og poster, samt følge drift og statistik i realtid.

## Hvad projektet er

Applikationen er bygget til operationel brug under afvikling af en festival:

- **Dashboard** til live overblik over check-ins, pit-status, udeblivelser og aktive alarmer.
- **Frivillig-modul** til håndtering af frivillige, vagttyper, konkrete vagter og bemandingsbehov.
- **Admin-modul** til brugeradministration, roller, import/eksport af data og statistik.
- **Profil-modul** til brugerprofil, avatar og farvetema.

Projektet er multi-sæson baseret, hvor data knyttes til `SeasonId` (år).

## Teknisk stack

- **.NET 10** (`net10.0`)
- **ASP.NET Core MVC + Razor Views**
- **Entity Framework Core 10 + SQLite**
- **ASP.NET Core Identity** (roller: `Administrator`, `Koordinator`)
- **Serilog** (Console + SQLite sink)
- **ClosedXML** til Excel import/eksport
- **Bootstrap + Bootstrap Icons + custom dark-mode design system**

## Arkitektur og opbygning

### Overordnet lagdeling

- **Controllers** (`web/Controllers`): HTTP endpoints og UI-flow.
- **Models** (`web/Models`): domænemodeller og viewmodels.
- **Data** (`web/Data`): `ApplicationDbContext` og identity seeding.
- **Views** (`web/Views`): Razor views/partials for UI.
- **wwwroot** (`web/wwwroot`): statiske filer (CSS, JS, libs).
- **Migrations** (`web/Migrations`): EF Core migrationer.
- **Persistensmapper**:
  - `web/App_dbs` (SQLite databaser)
  - `web/App_files` (uploads, fx avatarer)

### Centrale controllers

- `HomeController`: live drift (check-in/check-out, pit/post flytning, no-show, dashboard-data).
- `FrivilligController`: frivillige, vagttyper, vagter og bemandingsbehov.
- `AdminController`: brugerhåndtering, import/eksport, statistik, QR opslag.
- `AccountController`: login, logout, oprettelse af første admin.
- `ProfileController`: profilopdatering, avatar upload, farvetema.
- `PostController`: dynamiske poster/zoner på dashboard.
- `DashboardSettingController`: nøgle/værdi indstillinger pr. sæson.

### Datamodel (kerne)

- **Volunteer**: frivillig (nøgle, navn, kontakt, QR metadata).
- **ShiftType**: vagttype med tidsrum og required count.
- **Shift**: kobling mellem frivillig og vagttype.
- **VolunteerCheckIn**: check-in session med nuværende lokation.
- **VolunteerLocationLog**: bevægelseslog (CheckIn/Move/CheckOut).
- **Post**: operationel post/zone på dashboard.
- **DashboardSetting**: sæsonspecifikke settings.
- **AppUser**: identity bruger med display name, avatar metadata og color mode.

### Runtime-adfærd

Ved opstart sker følgende automatisk i `Program.cs`:

1. Mapperne `App_dbs` og DB-filer oprettes hvis de mangler.
2. EF migrationer køres (`Database.Migrate()`).
3. Roller seedes (`Administrator`, `Koordinator`).

Det betyder, at første start på en tom installation opretter databasen automatisk.

## Lokalt udviklingsflow

Kør fra `web/`:

```bash
dotnet restore
dotnet build
dotnet run
```

Standard dev-url (fra launch settings):

- `http://localhost:5157`
- `https://localhost:7029`

## Installation på server med Docker Compose

Nedenstående er den anbefalede måde at deploye på en Linux-server (VPS/dedikeret server).

### 1) Forudsætninger

Installer på serveren:

- Docker Engine
- Docker Compose plugin (`docker compose`)
- Git

### 2) Hent projektet

```bash
git clone https://github.com/hera83/Festival.git
cd Festival
```

### 3) Start applikationen

Opret først en lokal env-fil med mail credentials:

```bash
cp .env.example .env
```

Udfyld derefter `.env` med dine rigtige værdier (denne fil er git-ignoreret).

Kør fra repository root:

```bash
docker compose up -d --build
```

Applikationen eksponeres på port **8080** (kan ændres i `docker-compose.yml`).

Mail credentials læses via miljøvariabler i compose:

- `EMAIL_SMTP_HOST`
- `EMAIL_SMTP_USERNAME`
- `EMAIL_SMTP_PASSWORD`
- `EMAIL_SMTP_FROM_EMAIL`
- `EMAIL_IMAP_HOST`
- `EMAIL_IMAP_USERNAME`
- `EMAIL_IMAP_PASSWORD`

- URL: `http://SERVER_IP:8080`

### 4) Første login

Hvis der ikke findes brugere i databasen, sendes du automatisk til **Create First Admin**. Opret første admin-bruger derfra.

### 5) Drift-kommandoer

```bash
# Se logs
docker compose logs -f

# Stop
docker compose down

# Opdater kode + genbyg
git pull
docker compose up -d --build
```

## Data persistence i Docker

Compose-monteringer sikrer persistent data mellem container-restarts:

- `festival_app_dbs -> /app/App_dbs`
- `festival_app_files -> /app/App_files`

Her ligger:

- SQLite databaser (`festival.db`, `festival_logs.db`)
- uploads (fx avatar-filer)

Volumens kan inspiceres med:

```bash
docker volume ls
docker volume inspect festival_app_dbs
```

## Produktionsanbefalinger

- Kør bag reverse proxy (Nginx/Caddy/Traefik) med TLS.
- Begræns adgang via firewall (kun 80/443 offentligt).
- Tag backup af `data/app_dbs` og `data/app_files`.
- Overvej logrotation og monitorering af container.

## License

This project is licensed under the CC BY-NC 4.0 license.

You may use, modify and share the code for non-commercial purposes only.

## Projektstruktur (kort)

```text
Festival/
├─ docker-compose.yml
├─ README.md
└─ web/
   ├─ Controllers/
   ├─ Data/
   ├─ Models/
   ├─ Views/
   ├─ Migrations/
   ├─ wwwroot/
   ├─ App_dbs/
   ├─ App_files/
   ├─ Program.cs
   ├─ Dockerfile
   └─ web.csproj
```
