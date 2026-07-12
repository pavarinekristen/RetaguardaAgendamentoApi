# Deploy da API em VPS

Atualizado em: 07/07/2026.

## Objetivo

Publicar a API .NET em uma VPS para rodar 24h e atender:

- Desktop WPF quando sincronizar.
- Front web leve em `laudos.clinicaideia.com.br`.

Dominio alvo:

```text
api.clinicaideia.com.br
```

## Arquitetura de producao recomendada

```text
Internet
  |
  v
Nginx com HTTPS
  |
  v
API ASP.NET Core / Kestrel
  |
  v
MySQL
```

## Requisitos da VPS

Minimo recomendado para comecar:

- Linux Ubuntu LTS.
- 1 vCPU.
- 1 GB RAM minimo, 2 GB recomendado.
- 20 GB disco minimo.
- Acesso SSH.
- IP fixo/publico.
- Porta 80 e 443 liberadas.
- Porta 22 liberada apenas para administracao.

## Dependencias no servidor

- .NET Runtime ou ASP.NET Core Runtime 8.
- Nginx.
- MySQL ou acesso seguro a MySQL externo.
- Certbot/Let's Encrypt ou SSL equivalente.
- systemd para manter API rodando.

## Build local da API

No computador de desenvolvimento:

```powershell
cd C:\Users\krist\RetaguardaAgendamentoAPI
dotnet publish RetaguardaAgendamentoAPI\RetaguardaAgendamentoAPI.csproj -c Release -o C:\tmp\RetaguardaAgendamentoAPI-publish
```

Enviar o conteudo de:

```text
C:\tmp\RetaguardaAgendamentoAPI-publish
```

para a VPS, exemplo:

```text
/var/www/sparkcore-api
```

## Variaveis/configuracoes de producao

Configurar no servidor, nao no Git:

```text
ConnectionStrings__DefaultConnection
ConnectionStrings__AdminConnection
AgendaOperacionalDatabase
Email__Enabled
Email__Host
Email__Port
Email__Username
Email__Password
Email__From
Cors__AllowedOrigins__0=https://laudos.clinicaideia.com.br
```

Regra:

- Senhas, tokens e credenciais nao entram no repositorio.
- Configuracao varia por deploy e deve ficar fora do codigo.

## Exemplo de service systemd

Arquivo:

```text
/etc/systemd/system/sparkcore-api.service
```

Exemplo:

```ini
[Unit]
Description=SparkCore API
After=network.target

[Service]
WorkingDirectory=/var/www/sparkcore-api
ExecStart=/usr/bin/dotnet /var/www/sparkcore-api/RetaguardaAgendamentoAPI.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=sparkcore-api
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5000

[Install]
WantedBy=multi-user.target
```

Comandos:

```bash
sudo systemctl daemon-reload
sudo systemctl enable sparkcore-api
sudo systemctl start sparkcore-api
sudo systemctl status sparkcore-api
```

## Exemplo Nginx

Arquivo:

```text
/etc/nginx/sites-available/api.clinicaideia.com.br
```

Exemplo:

```nginx
server {
    listen 80;
    server_name api.clinicaideia.com.br;

    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

Ativar:

```bash
sudo ln -s /etc/nginx/sites-available/api.clinicaideia.com.br /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

## SSL

Com Certbot:

```bash
sudo certbot --nginx -d api.clinicaideia.com.br
```

Depois validar:

```text
https://api.clinicaideia.com.br/health
```

## Banco em producao

Opcao recomendada:

- MySQL na mesma VPS no inicio.
- Backup diario.
- Acesso externo fechado.

Alternativa:

- Banco gerenciado ou MySQL externo.
- Exige avaliar seguranca, latencia e custo.

## DNS

No painel DNS do dominio:

```text
api.clinicaideia.com.br -> A -> IP_DA_VPS
```

## Checklist de deploy

Antes:

- VPS criada.
- Dominio apontando.
- MySQL instalado/configurado.
- Migrations aplicadas.
- API publicada em Release.
- Variaveis de ambiente configuradas.
- CORS configurado.

Durante:

- Subir arquivos da API.
- Criar service systemd.
- Criar Nginx.
- Emitir SSL.
- Reiniciar API.

Depois:

- Testar `/health`.
- Testar `/auth/me` sem token, deve retornar 401.
- Testar login.
- Testar `GET /portal/clientes`.
- Testar front web apontando para a API publica.

