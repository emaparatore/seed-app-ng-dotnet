# Preparazione VPS per Deploy Docker

Questa guida prepara un VPS vuoto per ospitare applicazioni deployate con Docker Compose e CI/CD.

**Quando usarla:** il server parte da zero e devi configurare sistema operativo, utente `deploy`, SSH, Docker, firewall e directory base.

**Output atteso:** al termine hai un server accessibile via SSH con utente non-root, Docker funzionante, firewall configurato e directory root pronta per i deploy. Il dominio, Cloudflare, certificati, `.env`, GitHub Actions e primo deploy sono trattati nella guida di deploy.

## Architettura Prevista

```text
Utente --> Cloudflare/DNS --> VPS (80/443/8443) --> Docker Compose app
                                                   --> servizi interni
```

Il VPS espone solo le porte necessarie. PostgreSQL, API, web app, logging e monitoring girano nei container della singola applicazione e non vengono configurati in questa guida.

---

## 1. Scelta del Provider VPS

### Hetzner Cloud (raccomandato)

- **CX22**: 2 vCPU, 4 GB RAM, 40 GB SSD, circa 4,50 EUR/mese
- Datacenter in Germania e Finlandia
- Accetta carte Revolut/prepagate
- Ottimo rapporto qualita/prezzo
- Sito: https://www.hetzner.com/cloud

### Alternative

| Provider | Piano consigliato | Prezzo | Note |
|----------|------------------|--------|------|
| DigitalOcean | Basic Droplet 2 vCPU / 4 GB | circa 24 USD/mese | Ottima documentazione |
| Contabo | VPS S (4 vCPU / 8 GB) | circa 6 EUR/mese | Piu economico, meno supporto |
| OVH | VPS Starter | circa 7 EUR/mese | Datacenter EU |

### Requisiti minimi

- 2 vCPU, 4 GB RAM, 40 GB SSD
- Ubuntu 24.04 LTS
- IPv4 pubblico

---

## 2. Setup Iniziale del Server

### 2.1 Primo accesso

Dopo aver creato il VPS, accedi come root con la password ricevuta via email o con la chiave SSH configurata dal provider:

```bash
ssh root@TUO_IP_VPS
```

### 2.2 Aggiorna il sistema

```bash
apt update && apt upgrade -y
```

### 2.3 Crea un utente non-root

Non usare `root` per le operazioni quotidiane:

```bash
adduser deploy
usermod -aG sudo deploy
```

### 2.4 Configura le chiavi SSH

Dal computer locale, crea una chiave SSH dedicata per questo VPS. Dare un nome esplicito evita confusione se hai gia altre chiavi:

```bash
ssh-keygen -t ed25519 -f ~/.ssh/seed-vps -C "deploy@seed-vps"
```

Il comando crea due file:

- `~/.ssh/seed-vps` - chiave privata, non condividerla mai
- `~/.ssh/seed-vps.pub` - chiave pubblica, da copiare sul server

Copia la chiave pubblica sull'utente `deploy` del VPS:

```bash
ssh-copy-id -i ~/.ssh/seed-vps.pub deploy@TUO_IP_VPS
```

Alla prima copia e normale che venga chiesta la password dell'utente `deploy`. Serve solo per installare la chiave pubblica in `/home/deploy/.ssh/authorized_keys`.

Verifica di poter accedere usando quella chiave:

```bash
ssh -i ~/.ssh/seed-vps deploy@TUO_IP_VPS
```

Per evitare di specificare `-i` ogni volta, configura un alias SSH sul computer locale nel file `~/.ssh/config`:

```sshconfig
Host seed-vps
  HostName TUO_IP_VPS
  User deploy
  IdentityFile ~/.ssh/seed-vps
  IdentitiesOnly yes
```

Su Windows il file si trova di solito in `C:\Users\TUO_UTENTE\.ssh\config`. Deve chiamarsi `config`, senza estensione `.txt`.

Da questo momento puoi accedere con:

```bash
ssh seed-vps
```

### 2.5 Disabilita accesso password e root login

Sul server, modifica la configurazione SSH:

```bash
sudo nano /etc/ssh/sshd_config
```

Imposta queste opzioni:

```text
PasswordAuthentication no
PermitRootLogin no
```

Riavvia SSH:

```bash
sudo systemctl restart ssh
```

> **Attenzione**: prima di chiudere la sessione corrente, apri un nuovo terminale e verifica di poter accedere con la chiave SSH. Se qualcosa va storto e ti chiudi fuori, dovrai usare la console web del provider.

---

## 3. Installazione Docker

```bash
# Installa le dipendenze
sudo apt install -y ca-certificates curl gnupg

# Aggiungi la chiave GPG di Docker
sudo install -m 0755 -d /etc/apt/keyrings
sudo rm -f /etc/apt/keyrings/docker.asc /etc/apt/keyrings/docker.gpg
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg

# Aggiungi il repository Docker
echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
  $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
  sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

# Installa Docker e Compose
sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# Permetti all'utente deploy di usare Docker
sudo usermod -aG docker deploy
```

Esci e rientra per applicare il gruppo:

```bash
exit
ssh deploy@TUO_IP_VPS
```

Verifica:

```bash
docker --version
docker compose version
docker run --rm hello-world
```

---

## 4. Configurazione Firewall

Apri SSH, HTTP, HTTPS e la porta usata dallo staging quando Cloudflare inoltra verso un origin diverso da 443:

```bash
sudo ufw allow OpenSSH
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw allow 8443/tcp
sudo ufw enable
```

Verifica:

```bash
sudo ufw status
```

Output atteso:

```text
To                         Action      From
--                         ------      ----
OpenSSH                    ALLOW       Anywhere
80/tcp                     ALLOW       Anywhere
443/tcp                    ALLOW       Anywhere
8443/tcp                   ALLOW       Anywhere
```

---

## 5. Directory Root per i Deploy

La convenzione del seed e usare `/opt/<project-slug>`. Se non imposti nulla, il default e `/opt/seed-app`.

Per una prima app basata sul default:

```bash
sudo mkdir -p /opt/seed-app
sudo chown deploy:deploy /opt/seed-app
```

Per una nuova app con `PROJECT_SLUG=nuovo-progetto`:

```bash
sudo mkdir -p /opt/nuovo-progetto
sudo chown deploy:deploy /opt/nuovo-progetto
```

La guida di deploy spiega come creare `.env`, certificati, directory per ambienti `production` e `staging`, backup e configurazione CI/CD.

---

## 6. Login GHCR Opzionale

Il deploy automatico usa GitHub Actions e non richiede un login manuale permanente sul VPS. Per debug o pull manuali puoi autenticarti a GHCR:

```bash
echo "IL_TUO_GITHUB_PAT" | docker login ghcr.io -u TUO_GITHUB_USERNAME --password-stdin
```

Il token deve avere almeno scope `read:packages`.

---

## 7. Checklist di Fine Setup

Verifica che:

- `ssh deploy@TUO_IP_VPS` funzioni senza password
- `root` login e password login siano disabilitati
- `docker --version` e `docker compose version` funzionino con utente `deploy`
- `sudo ufw status` mostri `OpenSSH`, `80/tcp`, `443/tcp`, `8443/tcp`
- la directory `/opt/<project-slug>` esista e appartenga a `deploy`

Se tutti i punti sono ok, continua con [New Project Deploy Guide](new-project-deploy-guide.md).

---

## Operazioni Host-Level Utili

### Stato risorse

```bash
df -h
docker system df
docker stats
```

### Pulizia Docker

```bash
docker system prune -f
```

Usa `docker volume prune -f` solo se sei certo che i volumi non contengano database o certificati ancora necessari.

### Portainer opzionale

Portainer puo essere utile per ispezionare container e volumi, ma non e richiesto dal deploy del seed. Se lo usi, esponilo solo via SSH tunnel o dietro protezione equivalente, mai pubblicamente senza autenticazione forte.

---

## Troubleshooting VPS

### Non riesco ad accedere via SSH

Verifica dalla console web del provider:

- l'utente `deploy` esiste
- la chiave pubblica e in `/home/deploy/.ssh/authorized_keys`
- `/etc/ssh/sshd_config` non contiene errori
- il firewall permette `OpenSSH`

### Docker richiede sudo

L'utente non ha ancora applicato il gruppo `docker`. Esci e rientra:

```bash
exit
ssh deploy@TUO_IP_VPS
```

Poi verifica:

```bash
groups
docker ps
```

### Le porte 80/443 non rispondono

Questa guida apre il firewall, ma non avvia alcuna applicazione. Dopo il primo deploy, verifica i container seguendo [New Project Deploy Guide](new-project-deploy-guide.md) e [Troubleshooting](../operations/troubleshooting.md).
