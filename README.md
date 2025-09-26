# Desafio Técnico - Microsserviços

Este projeto é uma aplicação com arquitetura de microserviços para gerenciamento de estoque de produtos e vendas em uma plataforma de e-commerce. O sistema foi desenvolvido como parte de um desafio técnico para demonstrar habilidades em design e implementação de sistemas distribuídos.

## Descrição

O objetivo é gerenciar o estoque de produtos e as vendas de forma desacoplada e escalável. O sistema é composto por dois microserviços principais:

*   **Serviço de Estoque:** Responsável por operações relacionadas a produtos (criação, leitura, atualização) e controle de quantidade em estoque.
*   **Serviço de Vendas:** Responsável por processar pedidos de clientes, registrar vendas e se comunicar com o serviço de estoque para atualizar as quantidades.

A comunicação entre os clientes e os microserviços é centralizada por um **API Gateway**, que atua como um ponto de entrada único, roteando as requisições para o serviço apropriado.

## Arquitetura

```
+-----------------+      +-----------------+      +-----------------+
|                 |      |                 |      |                 |
|     Cliente     +------>  API Gateway    +------>  Serviço de     |
|      (UI)       |      |     (YARP)      |      |     Vendas      |
|                 |      |                 |      |                 |
+-----------------+      +-------+---------+      +--------+--------+
                                 |                         |
                                 |                         | (RabbitMQ)
                                 v                         v
                         +-------+---------+      +--------+--------+
                         |                 |      |                 |
                         | Serviço de      <------+  Serviço de     |
                         |    Estoque      |      |     Estoque     |
                         |                 |      |   (Consumidor)  |
                         +-----------------+      +-----------------+
```

### Descrição da Arquitetura

A arquitetura do sistema é baseada no padrão de microserviços, projetada para oferecer escalabilidade, resiliência e manutenibilidade. Os serviços são desacoplados e se comunicam através de chamadas de API síncronas e mensageria assíncrona.

*   **API Gateway (YARP):** É o ponto de entrada único para todas as requisições dos clientes. Ele é responsável por rotear o tráfego para o microserviço apropriado. Além do roteamento, ele centraliza responsabilidades transversais, como autenticação (via JWT).

*   **Microserviço de Vendas (Sales.API):** Orquestra toda a lógica de negócio relacionada a vendas. Suas responsabilidades incluem a criação de pedidos e a consulta de histórico. Ao receber um novo pedido, ele o persiste em seu próprio banco de dados e, em seguida, publica um evento para notificar outros serviços.

*   **Microserviço de Estoque (Stock.API):** Gerencia o ciclo de vida dos produtos. Ele expõe endpoints para operações de criação, leitura e atualização de produtos. Além disso, possui um *consumer* que escuta eventos da fila do RabbitMQ para atualizar a quantidade de produtos em estoque de forma assíncrona sempre que uma venda ocorre.

*   **RabbitMQ (Message Broker):** Atua como o barramento de comunicação assíncrona entre os serviços. Ele garante que a comunicação entre os serviços de Vendas e Estoque seja confiável e desacoplada. Se o serviço de Estoque estiver temporariamente indisponível, a mensagem de atualização de estoque permanecerá na fila para ser processada mais tarde, garantindo a consistência eventual dos dados.

## Tecnologias Utilizadas

*   **.NET 9/C# 13:** Plataforma de desenvolvimento para a construção dos microserviços.
*   **SQL Server 2022 Developer:** Banco de dados relacional para persistência de dados em cada serviço.
*   **RabbitMQ 4.1.4:** Message broker para comunicação assíncrona entre os serviços de Vendas e Estoque.
*   **YARP (Yet Another Reverse Proxy):** Utilizado como API Gateway para rotear e gerenciar o tráfego para os microserviços.

## Teste da API

Simulação do fluxo de compra:

**Passo 1: Obter um Token de Autenticação**

O Gateway protege as rotas. Obtenha um token JWT fazendo login (o `AuthController` possui um usuário de teste "admin" / "admin123").

  ```bash
  curl -X POST http://localhost:5101/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username": "admin", "password": "admin123"}'
  ```

*Copie o token retornado para usar nos próximos passos.*

**Passo 2: Criar um Produto**

Adicione um produto ao estoque.

  ```bash
  curl -X POST http://localhost:5101/stock/products \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer SEU_TOKEN_JWT" \
  -d '{"name": "Livro de Microserviços", "description": "Um livro incrível", "price": 99.90, "stockQuantity": 10}'
  ```

*Anote o `id` do produto retornado.*

**Passo 3: Realizar uma Venda**

Crie um pedido para o produto recém-criado.

```bash
  curl -X POST http://localhost:5101/sales/orders \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer SEU_TOKEN_JWT" \
  -d '{"customerId": "c1a2b3c4-d5e6-f7a8-b9c0-d1e2f3a4b5c6", "items": [{"productId": "ID_DO_PRODUTO_CRIADO", "quantity": 2}]}'
```

**Passo 4: Verificar a Baixa no Estoque**

Consulte o produto novamente para confirmar que a quantidade em estoque foi atualizada de 10 para 8.

```bash
  curl http://localhost:5101/stock/products/ID_DO_PRODUTO_CRIADO \
  -H "Authorization: Bearer SEU_TOKEN_JWT"
```

Você deverá ver `"stockQuantity": 8` na resposta. Isso confirma que o fluxo síncrono e assíncrono funcionou corretamente.
