# Task 1: Aggiungere mem_limit ai servizi esistenti

## Contesto

- `docker/docker-compose.deploy.yml` contiene 5 servizi production: postgres, seq, api, web, nginx
- Nessun servizio ha attualmente `mem_limit` configurato
- RAM server: 3.7GB totale, ~3.2GB disponibili per production (staging ~260MB, SO ~200MB)
- Uso reale attuale: PostgreSQL 22MB, API 70MB, Seq 117MB, Web 30MB, Nginx 4MB (totale ~243MB)

## Piano di esecuzione

### File da modificare
- `docker/docker-compose.deploy.yml`

### Approccio step-by-step

1. Aggiungere `mem_limit` a ogni servizio in `docker-compose.deploy.yml` secondo la tabella del piano:

   | Servizio   | mem_limit |
   |------------|-----------|
   | postgres   | 1536m     |
   | api        | 768m      |
   | seq        | 512m      |
   | web        | 384m      |
   | nginx      | 64m       |

2. Posizionare `mem_limit` dopo l'ultimo campo di ogni servizio (prima di `networks`, `depends_on`, o `healthcheck` — seguire lo stile esistente, aggiungere come proprietà top-level del servizio)

3. Validare la sintassi del file compose:
   ```bash
   cd /project/docker && docker compose -f docker-compose.deploy.yml config
   ```

### Test da verificare

- `docker compose -f docker-compose.deploy.yml config` non produce errori
- Ogni servizio ha il campo `mem_limit` corretto nell'output del config
- La somma dei limiti (1536+768+512+384+64 = 3264MB) rientra nella RAM disponibile (~3.2GB)

## Criteri di completamento

- Tutti e 5 i servizi in `docker-compose.deploy.yml` hanno `mem_limit` configurato con i valori specificati
- Il file compose è sintatticamente valido (verificato con `docker compose config`)
- Nessun altro campo è stato modificato

## Risultato

- **File modificati:** `docker/docker-compose.deploy.yml`
- **Scelte implementative e motivazioni:**
  - `mem_limit` posizionato come proprietà top-level di ogni servizio, prima di `healthcheck`, `networks` o `depends_on` secondo lo stile esistente del file
  - Valori esattamente come da tabella del piano: postgres=1536m, api=768m, seq=512m, web=384m, nginx=64m
  - Somma totale: 3264MB, rientra nei ~3.2GB disponibili per production
- **Deviazioni dal piano:**
  - La validazione con `docker compose config` non è stata possibile perché Docker non è disponibile nell'ambiente sandbox. La correttezza YAML è stata verificata visualmente (indentazione corretta a 4 spazi, sintassi `mem_limit: <valore>m` standard Docker Compose). La validazione completa andrà eseguita sul server di deploy.
