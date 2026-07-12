# Desktop WPF e Instalador

Atualizado em: 07/07/2026.

## Papel do desktop

O desktop SparkCore e a operacao principal da clinica.

Responsabilidades:

- Cadastro completo de cliente/funcionario.
- Pesquisa interna.
- Agenda.
- Status do agendamento.
- Laudos.
- Historico.
- Relatorios.
- Impressao.
- Backup local.
- Sincronizacao.

## Path

```text
C:\Users\krist\AgendamentoWpfApp
```

Solution:

```text
C:\Users\krist\AgendamentoWpfApp\AgendamentoWpfApp.sln
```

## Banco local

SQLite em:

```text
%LOCALAPPDATA%\RetaguardaAgendamento\agenda.sqlite
```

## Modulos implementados

- Login.
- Cadastro.
- Pesquisa.
- Agenda.
- Profissionais/salas.
- Laudos.
- Configuracoes.
- Backup.
- Sincronizacao automatica.

## Cadastro de cliente/funcionario

Campos principais:

- ID sequencial.
- Nome.
- Empresa.
- Escolaridade.
- Cargo.
- Estado civil.
- Naturalidade.
- Email.
- CPF.
- RG.
- Sexo.
- Tipo de endereco.
- Endereco.

Regra:

- ID sequencial e o identificador visivel.
- GUID `IdLocal` e tecnico.

## Agendamento

Campos principais:

- Data.
- Horario.
- Funcionario.
- Empresa.
- Motivo.
- Observacao.
- Trabalha armado.
- Status.

Tipos de laudo:

- Sem arma.
- Com arma.

Motivos iniciais:

- Admissao.
- Periodico.
- Retorno ao trabalho.
- Mudanca de funcao.
- Demissional.

## Laudos oficiais

Template:

```text
C:\Users\krist\AgendamentoWpfApp\Assets\Templates\laudos-oficiais.pdf
```

Regra:

- Pagina 1: sem arma.
- Pagina 2: com arma.

Dependencia:

```text
PdfSharpCore 1.3.67
```

Ao baixar:

- O sistema gera um PDF com apenas a pagina correta.
- O status pode ser marcado como `Baixado`.

## Build WPF

```powershell
dotnet build C:\Users\krist\AgendamentoWpfApp\AgendamentoWpfApp.sln --ignore-failed-sources
```

## Testes WPF

```powershell
dotnet run --project C:\Users\krist\AgendamentoWpfApp\AgendamentoWpfApp.Tests\AgendamentoWpfApp.Tests.csproj
```

## Publish self-contained

```powershell
dotnet publish C:\Users\krist\AgendamentoWpfApp\AgendamentoWpfApp.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o C:\tmp\AgendamentoWpfApp-selfcontained-test
```

## Instalador

Instalador atual registrado:

```text
C:\Users\krist\AgendamentoWpfApp\artifacts\installer\SparkCore-Setup-1.0.2.exe
```

Gerado com:

```text
Inno Setup
```

Script:

```text
C:\Users\krist\AgendamentoWpfApp\Installer\SparkCore.iss
```

## Pendencias do instalador

- Confirmar icone final.
- Confirmar nome final do produto.
- Confirmar pasta de instalacao.
- Confirmar se precisa instalar WebView2 Runtime.
- Confirmar estrategia de atualizacao futura.
- Confirmar se a URL da API sera configurada por instalador, arquivo ou variavel de ambiente.

