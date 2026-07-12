# Deploy da API SparkCore na Linode (substituindo o backend antigo)

Atualizado em: 10/07/2026. Este guia substitui a parte de banco do
`documentacao-sistema/07-deploy-vps-api.md` (que ainda citava MySQL — a API agora usa **Postgres**).

Servidor alvo: Linode `li927-43` (`45.56.77.43`), onde hoje roda o backend antigo na porta 9090.
Estratégia: subir a API nova **ao lado** do backend antigo (portas diferentes), validar, apontar o
DNS/front para a nova e só então desligar o antigo.

---

## 0. Pré-requisitos

- Acesso SSH à Linode (usuário com sudo).
- DNS: criar registro `A` `api.clinicaideia.com.br -> 45.56.77.43` no painel da Hostinger.
  (Pode criar já no início; a propagação anda enquanto você faz o resto.)
- Portas 80/443 liberadas no firewall da Linode.

## 1. Instalar dependências na VPS (Ubuntu)

```bash
# ASP.NET Core Runtime 8
sudo add-apt-repository ppa:dotnet/backports -y 2>/dev/null || true
sudo apt-get update
sudo apt-get install -y aspnetcore-runtime-8.0 nginx certbot python3-certbot-nginx

# Docker (para o Postgres)
curl -fsSL https://get.docker.com | sudo sh
```

Se `aspnetcore-runtime-8.0` não existir no feed da distro, usar o repositório da Microsoft
(packages.microsoft.com) conforme docs oficiais do .NET para a versão do Ubuntu.

## 2. Subir o Postgres de produção

```bash
sudo mkdir -p /opt/sparkcore/postgres
# copiar para lá: deploy/docker-compose.postgres.prod.yml
cd /opt/sparkcore/postgres
echo "POSTGRES_ROOT_PASSWORD=UMA_SENHA_FORTE_AQUI" | sudo tee .env >/dev/null
sudo chmod 600 .env
sudo docker compose -f docker-compose.postgres.prod.yml up -d
sudo docker ps   # aguardar healthy
```

## 3. Aplicar migrations e trocar senhas das roles

```bash
sudo mkdir -p /opt/sparkcore/deploy
# copiar para a VPS: a pasta postgres-migrations/ e deploy/apply-postgres-migrations.sh
cd /opt/sparkcore/deploy
chmod +x apply-postgres-migrations.sh
./apply-postgres-migrations.sh /opt/sparkcore/postgres-migrations
```

**Obrigatório em produção** (o 002 cria as roles com senha de desenvolvimento):

```bash
sudo docker exec -it agenda-db-postgres psql -U postgres -d retaguarda_agendamento \
  -c "ALTER ROLE agenda_user  PASSWORD 'SENHA_FORTE_USER';"
sudo docker exec -it agenda-db-postgres psql -U postgres -d retaguarda_agendamento \
  -c "ALTER ROLE agenda_admin PASSWORD 'SENHA_FORTE_ADMIN';"
```

## 4. (Opcional) Levar os dados do Postgres local para a VPS

Só faça isso se quiser subir com as contas/dados que existem hoje na máquina local
(migrados do MySQL em 10/07/2026: 2 empresas, 7 usuários, 1003 clientes, 1003 consultas).
Se preferir começar limpo, pule: contas são criadas via API e o WPF sincroniza os dados.

No Windows local:

```powershell
docker exec agenda-db-postgres pg_dump -U postgres -d retaguarda_agendamento --data-only --disable-triggers -n retaguarda_agendamento -n agenda_operacional -f /tmp/dados.sql
docker cp agenda-db-postgres:/tmp/dados.sql C:\tmp\dados.sql
scp C:\tmp\dados.sql usuario@45.56.77.43:/tmp/dados.sql
```

Na VPS (as tabelas finais de dados — clientes, consultas, agendamentos, profissionais_salas —
não existem no schema baseline; o dump local **não** as inclui no DDL, então gere o schema delas
antes, ou faça o dump completo sem `--data-only`):

```bash
# caminho mais simples: dump completo do schema agenda_operacional local (com DDL) e restore
sudo docker cp /tmp/dados.sql agenda-db-postgres:/tmp/dados.sql
sudo docker exec agenda-db-postgres psql -U postgres -d retaguarda_agendamento -v ON_ERROR_STOP=1 -f /tmp/dados.sql
```

> Recomendação prática: para levar tudo com DDL incluído, use no Windows
> `pg_dump ... --clean --if-exists -n retaguarda_agendamento -n agenda_operacional`
> (sem `--data-only`) e reaplique o `002_roles.sql` + `ALTER ROLE` depois do restore.

## 5. Publicar a API

No Windows local:

```powershell
cd C:\Users\krist\RetaguardaAgendamentoAPI
dotnet publish RetaguardaAgendamentoAPI\RetaguardaAgendamentoAPI.csproj -c Release -o C:\tmp\sparkcore-api-publish
scp -r C:\tmp\sparkcore-api-publish\* usuario@45.56.77.43:/tmp/sparkcore-api/
```

Na VPS:

```bash
sudo mkdir -p /var/www/sparkcore-api
sudo rsync -a --delete /tmp/sparkcore-api/ /var/www/sparkcore-api/
sudo chown -R www-data:www-data /var/www/sparkcore-api
```

## 6. Configurar variáveis de ambiente e systemd

```bash
sudo mkdir -p /etc/sparkcore
# copiar deploy/sparkcore-api.env.example -> /etc/sparkcore/sparkcore-api.env e PREENCHER
sudo chmod 600 /etc/sparkcore/sparkcore-api.env

# copiar deploy/sparkcore-api.service -> /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now sparkcore-api
sudo systemctl status sparkcore-api --no-pager
curl -s http://127.0.0.1:5000/health   # esperado: {"status":"OK",...}
```

O `appsettings.Production.json` deixa as connection strings vazias de propósito:
se o env não estiver preenchido a API não conecta em lugar nenhum — nunca cai por engano
no banco localhost de desenvolvimento.

## 7. Nginx + SSL

```bash
# copiar deploy/nginx-api.clinicaideia.com.br.conf
sudo cp nginx-api.clinicaideia.com.br.conf /etc/nginx/sites-available/api.clinicaideia.com.br
sudo ln -s /etc/nginx/sites-available/api.clinicaideia.com.br /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx

# com o DNS já propagado:
sudo certbot --nginx -d api.clinicaideia.com.br
```

## 8. Validação

```bash
curl -s https://api.clinicaideia.com.br/health            # 200 OK
curl -s -o /dev/null -w "%{http_code}\n" https://api.clinicaideia.com.br/auth/me   # 401
```

- Login no front: publicar `web-laudos/` na Hostinger em
  `/home/u183827986/domains/clinicaideia.com.br/public_html/laudos`
  com `config.js` apontando para `https://api.clinicaideia.com.br`.
- WPF em máquina de cliente: `AGENDAMENTO_RETAGUARDA_URL=https://api.clinicaideia.com.br`
  e validar uma sincronização completa.

## 9. Desligar o backend antigo (porta 9090)

Só depois de tudo validado:

```bash
sudo ss -tlnp | grep 9090          # identificar o processo/serviço antigo
sudo systemctl disable --now NOME_DO_SERVICO_ANTIGO
```

Manter os dados/arquivos do sistema antigo até confirmar que nada mais depende dele.

## 10. Backup (mínimo viável)

Cron diário na VPS:

```bash
sudo crontab -e
# 0 3 * * * docker exec agenda-db-postgres pg_dump -U postgres -d retaguarda_agendamento | gzip > /opt/sparkcore/backups/retaguarda-$(date +\%F).sql.gz
```

Criar `/opt/sparkcore/backups` antes e limitar retenção (ex.: `find ... -mtime +14 -delete`).
