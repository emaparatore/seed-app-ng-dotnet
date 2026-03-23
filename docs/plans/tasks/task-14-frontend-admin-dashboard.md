# Task 14: Frontend — Dashboard admin

## Contesto

- **Backend API pronto:** `GET /api/v1.0/admin/dashboard` restituisce `DashboardStatsDto` con: `TotalUsers`, `ActiveUsers`, `InactiveUsers`, `RegistrationsLast7Days`, `RegistrationsLast30Days`, `RegistrationTrend` (30 `DailyRegistrationDto`), `UsersByRole` (`RoleDistributionDto[]`), `RecentActivity` (5 `RecentActivityDto`)
- **Routing esistente:** `admin.routes.ts` punta a `AdminPlaceholder` per la rotta `dashboard` — va sostituito col nuovo componente
- **Permesso:** `Dashboard.ViewStats` (già configurato nel guard della rotta)
- **Layout admin:** `AdminLayout` con sidebar 240px + content area con padding 24px
- **Nessun servizio API admin nel frontend:** serve creare un `AdminDashboardService` (o service generico admin)
- **Pattern frontend:** standalone components, Angular signals, Vitest, Material 3 con CSS variables (`--mat-sys-*`), `AUTH_CONFIG` per apiUrl base (`environment.apiUrl`)
- **Decisione piano:** grafici SVG custom (Option C), zero dipendenze esterne, componenti Angular isolati

## Piano di esecuzione

### Step 1: Creare modelli TypeScript per la risposta API

**File da creare:** `frontend/web/projects/app/src/app/pages/admin/dashboard/models/dashboard.models.ts`

```typescript
export interface DashboardStats {
  totalUsers: number;
  activeUsers: number;
  inactiveUsers: number;
  registrationsLast7Days: number;
  registrationsLast30Days: number;
  registrationTrend: DailyRegistration[];
  usersByRole: RoleDistribution[];
  recentActivity: RecentActivity[];
}

export interface DailyRegistration {
  date: string; // ISO date (DateOnly serializza come "2026-03-23")
  count: number;
}

export interface RoleDistribution {
  roleName: string;
  userCount: number;
}

export interface RecentActivity {
  id: string;
  timestamp: string;
  action: string;
  entityType: string;
  userId: string | null;
}
```

### Step 2: Creare il servizio API per la dashboard

**File da creare:** `frontend/web/projects/app/src/app/pages/admin/dashboard/dashboard.service.ts`

- Injectable in `root`
- Inietta `HttpClient` e `AUTH_CONFIG` per costruire l'URL: `${apiUrl}/admin/dashboard`
- Metodo `getStats(): Observable<DashboardStats>`

### Step 3: Creare il componente line chart SVG custom

**File da creare:** `frontend/web/projects/app/src/app/pages/admin/dashboard/components/line-chart.ts`

- Standalone component, zero dipendenze
- Input: `data: DailyRegistration[]` (signal input)
- SVG inline nel template con viewBox responsive
- Disegna polyline con i punti (x = giorno, y = count), griglia opzionale, etichette asse X (date) e Y (conteggi)
- Colore primario da CSS variable `--mat-sys-primary`
- Tooltip on hover (opzionale, semplice title attribute sui punti)

### Step 4: Creare il componente donut chart SVG custom

**File da creare:** `frontend/web/projects/app/src/app/pages/admin/dashboard/components/donut-chart.ts`

- Standalone component, zero dipendenze
- Input: `data: RoleDistribution[]` (signal input)
- SVG con archi calcolati via `stroke-dasharray`/`stroke-dashoffset` su elementi `<circle>`
- Legenda sotto il grafico (pallino colorato + nome ruolo + count)
- Palette colori da CSS variables Material o array fisso

### Step 5: Creare il componente dashboard principale

**File da creare:** `frontend/web/projects/app/src/app/pages/admin/dashboard/dashboard.ts`

- Standalone component
- Inietta `AdminDashboardService`
- Stato: `loading = signal(true)`, `stats = signal<DashboardStats | null>(null)`, `error = signal<string | null>(null)`
- `ngOnInit`: chiama il servizio, setta i signal
- Template (file separato o inline):
  - **Skeleton loading:** 5 card placeholder + 2 chart placeholder con animazione pulse (CSS)
  - **Sezione card statistiche** (grid responsive):
    - Utenti totali (icon `people`)
    - Utenti attivi (icon `person`, colore verde)
    - Utenti disattivati (icon `person_off`, colore rosso)
    - Registrazioni ultimi 7 giorni (icon `person_add`)
    - Registrazioni ultimi 30 giorni (icon `person_add`)
  - **Sezione grafici** (grid 2 colonne su desktop, 1 su mobile):
    - Line chart trend registrazioni (30 giorni)
    - Donut chart distribuzione ruoli
  - **Sezione ultime attività:**
    - Lista delle 5 attività recenti (timestamp, azione, tipo entità)
    - Link "Vedi tutto" che naviga a `/admin/audit-log`
- **Responsive:** CSS Grid con `auto-fit` / `minmax` per le card, stack su mobile
- Usa `MatCardModule`, `MatIconModule`, `MatButtonModule`, `RouterLink`

### Step 6: Aggiornare il routing

**File da modificare:** `frontend/web/projects/app/src/app/pages/admin/admin.routes.ts`

- Cambiare il lazy load della rotta `dashboard` per puntare al nuovo componente `Dashboard` invece di `AdminPlaceholder`

### Step 7: Scrivere i test

**File da creare:** `frontend/web/projects/app/src/app/pages/admin/dashboard/dashboard.spec.ts`

Test cases:
1. Il componente viene creato
2. Chiama `getStats()` all'init e mostra i dati
3. Mostra lo skeleton loading durante il caricamento
4. Mostra messaggio di errore se la chiamata fallisce
5. Le card mostrano i valori corretti (totalUsers, activeUsers, etc.)
6. Il link "Vedi tutto" punta a `/admin/audit-log`

**File da creare:** `frontend/web/projects/app/src/app/pages/admin/dashboard/components/line-chart.spec.ts`

Test cases:
1. Il componente viene creato
2. Renderizza l'SVG con i punti corretti dato un array di dati

**File da creare:** `frontend/web/projects/app/src/app/pages/admin/dashboard/components/donut-chart.spec.ts`

Test cases:
1. Il componente viene creato
2. Renderizza la legenda con i nomi dei ruoli

### Step 8: Verificare build e test

```bash
cd frontend/web && ng test app
cd frontend/web && ng build app
```

## Criteri di completamento

- [ ] Card con conteggi: utenti totali, attivi, disattivati
- [ ] Card con registrazioni: ultimi 7 e 30 giorni
- [ ] Grafico trend registrazioni (line chart SVG custom, componente isolato)
- [ ] Grafico distribuzione utenti per ruolo (donut chart SVG custom, componente isolato)
- [ ] Widget ultime 5 attività audit con link a sezione completa
- [ ] Skeleton loading durante il caricamento
- [ ] Responsive: card si riorganizzano su tablet/mobile
- [ ] Rotta `/admin/dashboard` carica il nuovo componente (non il placeholder)
- [ ] Test passano (`ng test app`)
- [ ] Build compila senza errori (`ng build app`)

## Risultato

### File creati
- `frontend/web/projects/app/src/app/pages/admin/dashboard/models/dashboard.models.ts` — Interfacce TypeScript per DashboardStats, DailyRegistration, RoleDistribution, RecentActivity
- `frontend/web/projects/app/src/app/pages/admin/dashboard/dashboard.service.ts` — AdminDashboardService con metodo getStats()
- `frontend/web/projects/app/src/app/pages/admin/dashboard/components/line-chart.ts` — Componente SVG standalone per trend registrazioni (polyline + area fill + tooltip)
- `frontend/web/projects/app/src/app/pages/admin/dashboard/components/donut-chart.ts` — Componente SVG standalone per distribuzione ruoli (archi via stroke-dasharray + legenda)
- `frontend/web/projects/app/src/app/pages/admin/dashboard/dashboard.ts` — Componente principale dashboard con signal state management
- `frontend/web/projects/app/src/app/pages/admin/dashboard/dashboard.html` — Template con stats cards, charts grid, activity list
- `frontend/web/projects/app/src/app/pages/admin/dashboard/dashboard.scss` — Stili responsive con CSS Grid auto-fit, skeleton loading, error state
- `frontend/web/projects/app/src/app/pages/admin/dashboard/dashboard.spec.ts` — 6 test cases (creazione, caricamento dati, skeleton, errore, valori card, link audit-log)
- `frontend/web/projects/app/src/app/pages/admin/dashboard/components/line-chart.spec.ts` — 2 test cases (creazione, rendering SVG points)
- `frontend/web/projects/app/src/app/pages/admin/dashboard/components/donut-chart.spec.ts` — 2 test cases (creazione, rendering legenda)

### File modificati
- `frontend/web/projects/app/src/app/pages/admin/admin.routes.ts` — Rotta dashboard ora punta a `Dashboard` invece di `AdminPlaceholder`
- `frontend/web/projects/app/src/app/pages/login/login.spec.ts` — Fix pre-esistente: aggiunto `mustChangePassword` al tipo mock di authService (bug non correlato al task)

### Scelte implementative e motivazioni
- **Signal inputs** (`input.required<T>()`) per i chart components: seguono il pattern Angular signals del progetto
- **AUTH_CONFIG** per API URL nel service: stesso pattern usato da AuthService
- **Colori chart da CSS variables Material** (`--mat-sys-primary`): coerenza col tema dell'app
- **Donut chart con `stroke-dasharray`/`stroke-dashoffset`**: tecnica SVG leggera che evita calcoli trigonometrici complessi per gli archi
- **Line chart con area fill**: migliora la leggibilità visiva del trend
- **CSS Grid con `auto-fit` + `minmax`**: responsive senza media queries, le card si riorganizzano automaticamente
- **Skeleton loading con animazione pulse CSS**: feedback visivo durante il caricamento senza librerie esterne
- **Template esterno** (dashboard.html): template troppo grande per inline, coerente con gli altri componenti del progetto

### Deviazioni dal piano
- **Fix login.spec.ts**: il test pre-esistente aveva un errore di tipo (mancava `mustChangePassword` nel mock). Corretto per permettere l'esecuzione della test suite completa. Non correlato al task ma necessario per la verifica.
