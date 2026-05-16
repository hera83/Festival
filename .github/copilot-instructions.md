# Copilot Instructions

## Project Guidelines
- Festival Vagtstyring design system: Brug altid det etablerede dark-mode design system fra wwwroot/css/site.css til alle nye sider og komponenter. Nøglepunkter: mørk baggrund (#0d1117), surface (#161b22), brand-farve orange (#e85d2e), statusfarver ok/warn/alarm/info, Inter-font, Bootstrap Icons. Brug CSS-variablerne: --clr-bg, --clr-surface, --clr-brand, --clr-text, --clr-text-muted, --clr-ok, --clr-warn, --clr-alarm, --clr-info. Brug komponentklasserne: .page-header, .card, .card-header, .card-body, .badge-ok/warn/alarm/info, .auth-wrapper, .auth-card. Login-siden bruger Layout = null og auth-wrapper/auth-card. Projektet er et live operations/dispatch system til festival-vagtstyring med frivillige.
- Ved import af frivillige: SeasonId (År) skal altid sættes til indeværende år på importtidspunktet, uanset vagternes datoer i regnearket.
- Brug altid pæne Bootstrap modals til bekræftelse og formularer – aldrig browser confirm(). Alle knapper i modals skal have Bootstrap Icons og være små (btn-sm stil). Design skal følge dark-mode design systemet.
- Alle alerts/notifikationer skal bruge den globale `showToast()` funktion i stedet for inline Bootstrap alerts. Brug `TempData["Success"]`, `TempData["Error"]`, `TempData["Warning"]` eller `TempData["Info"]` fra controllere. Brug `showToast(message, type)` direkte fra JavaScript. Aldrig brug inline alert-divs på sider.
- Brug ikke ViewData["Title"] i dette projekt; undgå ViewData til sidetitler.

## Table Standards
- Alle tabeller skal følge dette standardmønster medmindre andet er specificeret:
  - **Over tabellen:** `d-flex justify-content-between` med søgefelt (input-group med bi-search ikon, keyup + 250ms debounce, fetch uden sidereload, nulstiller til side 1) til venstre og en handlingsknap (fx "Opret X") til højre.
  - **Kortet:** `.card` med `.card-header` der viser ikon + navn + totalt antal i parentes (TotalCount, ikke kun aktuel side). Tabel bruger `.table .table-hover` med `color: var(--clr-text)`.
  - **Spinner + tom tilstand:** Øjeblikkelig `spinner-border-sm` i `--clr-brand` i tbody ved keyup. Tom tilstand viser relevant ikon + "Ingen X fundet" tekst.
  - **Pagineringsbar under tabellen:** `d-flex justify-content-between`:
    - Venstre: `RangeFrom–RangeTo af TotalCount` – altid synlig
    - Høyre: "Pr. side" dropdown (10/25/50, default 10) + sidevælger med ‹/› knapper (altid synlige, disabled ved grænser), sidenumre med … ellipsis, aktiv side i `--clr-brand`.
  - **Server-side:** Controller-action tager `q`, `page`, `pageSize`. ViewModel har `Page`, `PageSize`, `TotalCount`, `TotalPages`, `RangeFrom`, `RangeTo`. Partial view opdaterer tbody + pagineringsbar + tæller via DOMParser på fetch-respons.