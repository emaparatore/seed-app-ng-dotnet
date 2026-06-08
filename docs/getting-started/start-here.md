# Start Here

Usa questa pagina come punto di ingresso alla documentazione del progetto. I documenti sotto non hanno tutti lo stesso scopo: alcuni servono per creare una nuova app dal seed, altri per configurare moduli opzionali, altri ancora per operazioni su un'app gia deployata.

## Scegli il tuo scenario

| Se devi fare questo... | Leggi questo documento |
|---|---|
| Provare il progetto in locale | [README](../../README.md) |
| Creare rapidamente una nuova app dal seed | [Seed Checklist](seed-checklist.md) |
| Creare e deployare una nuova app dal seed con guida completa | [New Project Deploy Guide](new-project-deploy-guide.md) |
| Preparare un VPS da zero | [VPS Setup Guide](vps-setup-guide.md) |
| Capire come funziona la pipeline di build/deploy | [CI/CD](../operations/ci-cd.md) |
| Configurare l'invio reale di email | [SMTP Configuration](../modules/smtp-configuration.md) |
| Attivare il modulo pagamenti / Stripe | [Stripe Payments Setup](../modules/stripe-payments-setup.md) |
| Completare gli adempimenti GDPR prima del go-live | [GDPR Compliance Checklist](../compliance/gdpr-compliance-checklist.md) |
| Gestire rollback, monitoring o troubleshooting | [Rollback](../operations/rollback.md), [Monitoring](../operations/monitoring.md), [Troubleshooting](../operations/troubleshooting.md) |

## Percorsi consigliati

### 1. Vuoi usare il seed per una nuova app

1. Leggi [Seed Checklist](seed-checklist.md)
2. Se ti serve il dettaglio completo, passa a [New Project Deploy Guide](new-project-deploy-guide.md)
3. Se il server non esiste ancora, usa [VPS Setup Guide](vps-setup-guide.md)
4. Attiva solo dopo i moduli opzionali che ti servono davvero, come SMTP o Stripe

### 2. Hai gia clonato il progetto e vuoi usare una capability

- Email reali: [SMTP Configuration](../modules/smtp-configuration.md)
- Pagamenti: [Stripe Payments Setup](../modules/stripe-payments-setup.md)
- Compliance: [GDPR Compliance Checklist](../compliance/gdpr-compliance-checklist.md)

### 3. Hai gia deployato l'app e ti servono operazioni runtime

- Pipeline e deploy: [CI/CD](../operations/ci-cd.md)
- Rollback: [Rollback](../operations/rollback.md)
- Monitoring: [Monitoring](../operations/monitoring.md)
- Problemi ricorrenti: [Troubleshooting](../operations/troubleshooting.md)

## Regola pratica

Se stai facendo nascere una nuova app dal seed, parti dai documenti di bootstrap.
Se stai attivando una feature specifica, leggi solo la guida verticale di quella feature.
Se l'app e gia in esercizio, usa i runbook operativi.
