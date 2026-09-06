# Desempenho no Render Free e no Neon Free

## Gargalos encontrados no código

As imagens são armazenadas em Base64 dentro de `SheetData` (JSONB). Antes, a consulta da listagem carregava todas as colunas de todas as fichas, mesmo que a resposta da API mostrasse apenas nomes e datas. A validação de senha também carregava a ficha inteira para usar apenas `EditPasswordHash`.

Isso transferia dados desnecessários do Neon para o Render e aumentava o uso de memória do servidor. Uma imagem de 500 KiB ocupa aproximadamente 667 KiB em Base64: dez fichas com imagens desse tamanho faziam a listagem buscar cerca de 6,5 MiB somente em imagens, antes de descartá-las. É um exemplo de volume, não uma medição das fichas de produção.

## Alterações

| Operação | Antes | Agora |
| --- | --- | --- |
| Listar fichas | Todos os campos, JSON, imagens e hashes | Somente ID, nome e datas, selecionados no PostgreSQL |
| Verificar senha | Ficha inteira | Somente o hash da senha |
| Abrir ficha | Ficha inteira, incluindo hash e rastreamento de alterações | Somente campos públicos, sem rastreamento |
| Salvar ficha | Baixava a versão anterior completa e reenviava todas as colunas | Consulta o hash e altera apenas JSON e data de atualização |
| Excluir ficha | Baixava a ficha inteira | Consulta o hash e exclui diretamente |
| Redefinir senha | Baixava e regravava a ficha inteira | Altera apenas hash e data em um comando |
| Transferir ficha ao navegador | Sem compactação configurada na aplicação | Brotli ou gzip, conforme o navegador, com nível rápido |
| Iniciar servidor com banco atualizado | Entrava no processo de migração a cada início | Verifica pendências e migra somente quando necessário |

As rotas, os campos JSON, as mensagens, o custo do BCrypt e as exigências de autenticação continuam iguais. HTML, CSS e JavaScript não foram alterados. Não há novas migrações, serviços pagos nem cache de fichas/senhas que possa devolver dados antigos.

Salvar e excluir também conferem no próprio comando se o hash ainda é o mesmo que foi validado. Se a senha for redefinida entre a validação e a gravação, a operação anterior é recusada. A redefinição não regrava uma cópia antiga do JSON.

A compactação é aplicada somente aos GETs de fichas e aos arquivos estáticos conhecidos. Respostas de login e operações de senha ficam fora dela. Imagens já compactadas têm ganho menor; o Base64 e o JSON ainda podem diminuir. As respostas são descompactadas automaticamente pelo navegador.

## Limites que o código não elimina

- **Render Free:** após 15 minutos sem tráfego de entrada, o serviço é suspenso. A documentação informa cerca de um minuto para reativação. Isso acontece antes de a aplicação conseguir responder e pode continuar perceptível no primeiro acesso. [Render: Spinning down on idle](https://render.com/docs/free#spinning-down-on-idle).
- **Neon Free:** o banco suspende após cinco minutos inativo, configuração fixa no plano gratuito. A primeira consulta precisa reativá-lo, normalmente em algumas centenas de milissegundos. [Neon: Scale to Zero](https://neon.com/docs/introduction/scale-to-zero).
- **BCrypt:** validar senhas exige CPU intencionalmente. As otimizações retiram o download desnecessário da ficha, mas não reduzem a proteção das senhas.
- **Regiões:** distância entre Render e Neon acrescenta latência às consultas. O projeto `dnd-sheets-db` foi confirmado no Neon em `aws-sa-east-1` (São Paulo). A região do Render ainda não foi verificada.

Não foram adicionados pings para manter os serviços acordados. Isso consumiria a franquia gratuita continuamente. O driver Npgsql já reutiliza conexões por padrão; não era necessário instalar outro serviço ou criar uma conexão permanente. [Npgsql: Connection String Parameters](https://www.npgsql.org/doc/connection-string-parameters.html).

## Publicação e comparação

O deploy normal pelo Dockerfile existente aplica as melhorias, sem novas variáveis de ambiente. Esta revisão altera os arquivos locais; não publica no Render nem modifica o Neon de produção.

Para comparar depois da publicação:

1. Abra o site após mais de 15 minutos parado e anote o tempo da primeira abertura.
2. Com o site já ativo, compare listar, abrir, liberar edição e salvar a mesma ficha.
3. No painel **Rede / Network** do navegador, confira duração e tamanho transferido de `/api/sheets`, `/api/sheets/{id}` e `/verify-password`. GETs podem mostrar `Content-Encoding: br` ou `gzip`.
4. Compare em condições semelhantes, com as mesmas fichas e rede. A redução da consulta de listagem acontece entre banco e servidor; o JSON pequeno mostrado no navegador já era pequeno antes.

Não foi medido o tempo de resposta de produção. Os ganhos exatos dependem do tamanho das fichas, região, rede, CPU disponível e estado ativo/suspenso dos serviços.

### Demora especificamente para liberar a edição

Após enviar a senha, o navegador aguarda `/api/sheets/{id}/verify-password` terminar para habilitar os campos. Essa consulta agora busca apenas o hash da senha. A resposta é pequena, então compactação não é a principal melhoria dessa etapa.

O backend registra uma linha `Edit password verification` nos logs do Render com três durações em milissegundos:

- `database_ms`: espera da consulta ao banco, incluindo conexão, rede e eventual reativação.
- `bcrypt_ms`: cálculo local que confere a senha.
- `total_ms`: total dentro do serviço de verificação; não inclui todo o processamento HTTP nem o trajeto navegador/Render.

O registro não inclui senha, hash, token ou conteúdo da ficha. Se o tempo total for baixo, mas o navegador esperar vários segundos, é necessário investigar o restante da requisição e a rede. Se `database_ms` ou `bcrypt_ms` dominar, o log identifica qual etapa merece investigação.

Em uma medição local com senha sintética, a biblioteca instalada (BCrypt.Net-Next 4.1.0, custo padrão 11) levou 104,4 ms na primeira verificação e média de 102,3 ms nas dez seguintes. Esse resultado não representa o desempenho do Render nem comprova a causa da demora em produção. O custo de proteção da senha foi mantido.

Na inspeção somente de leitura do Neon em 06/09/2026, foram encontradas dez fichas, todas com custo BCrypt 11 e hashes de 60 bytes. O JSON tinha média de 163.710 bytes, máximo de 444.571 bytes e total de 1.637.096 bytes. Antes, esse JSON também era baixado ao validar a senha; agora a consulta retorna apenas os 60 bytes do hash, além do protocolo da conexão.

Uma amostra de `EXPLAIN ANALYZE` no banco já ativo levou 0,047 ms com a seleção antiga e 0,022 ms com a seleção do hash. Esses tempos medem a execução interna do plano, não a transferência e serialização da ficha, a conexão Render–Neon nem o BCrypt no Render. Não são um benchmark completo da liberação de edição. O histórico de consultas não estava disponível por ausência de `pg_stat_statements`; nenhuma extensão foi instalada e nenhum dado ou configuração do banco foi alterado.

## Testes locais

Os testes usam PostgreSQL real e a API ASP.NET Core completa. Cobrem projeções SQL, autenticação, respostas públicas sem hashes, senhas certas/erradas, criação, atualização, exclusão, redefinição e preservação do JSON. Também verificam revogação de gravações após redefinição e compactação sem perda em HTTPS com Brotli e gzip, usando dados equivalentes a uma imagem de 500 KiB.

É necessário um PostgreSQL local e descartável. O teste recusa hosts diferentes de `localhost`/`127.0.0.1` e nomes de banco que não comecem com `dnd_test_`. As migrações e os dados de teste são criados nesse banco, que deve ser exclusivo dos testes.

Exemplo no PowerShell, com as credenciais de seu PostgreSQL local:

```powershell
$env:DND_TEST_CONNECTION = 'Host=127.0.0.1;Port=5432;Database=dnd_test_performance;Username=postgres;Password=SENHA_LOCAL;SSL Mode=Disable'
dotnet test backend.Tests/DnDSheetApi.Tests.csproj -c Release
```

O usuário local precisa ter permissão para criar o banco, ou o banco vazio deve ser criado previamente. Os testes deixam dados sintéticos no banco descartável; não use um túnel que aponte localhost para produção.
