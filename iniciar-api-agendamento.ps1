$ErrorActionPreference = "Stop"

Set-Location "C:\Users\krist\RetaguardaAgendamentoAPI"

docker compose -f docker-compose.mysql.yml up -d db_mysql
docker ps --filter "name=agenda-db-mysql"

$env:ASPNETCORE_URLS = "http://localhost:5000"
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:AGENDA_OPERACIONAL_DATABASE = "agenda_operacional"
$env:ConnectionStrings__DefaultConnection = "Server=localhost;Port=3308;Database=retaguarda_agendamento;Uid=agenda_user;Pwd=AgendaUser@2026;"
$env:ConnectionStrings__AdminConnection = "Server=localhost;Port=3308;Uid=agenda_admin;Pwd=AgendaAdmin@2026;"

dotnet run --project RetaguardaAgendamentoAPI\RetaguardaAgendamentoAPI.csproj --urls "http://localhost:5000"
