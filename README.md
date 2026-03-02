# FinSight 💰

**AI-powered Personal Finance Intelligence Platform** — A production-grade .NET 8 REST API built with Clean Architecture, DDD, CQRS, JWT auth, and a full CI/CD pipeline.

## 🏗️ Architecture

```
FinSight/
├── src/
│   ├── FinSight.Domain/          # Zero dependencies — pure C# domain model
│   ├── FinSight.Application/     # CQRS handlers, validators, interfaces
│   ├── FinSight.Infrastructure/  # EF Core, PostgreSQL, JWT, AI, Plaid
│   └── FinSight.Api/             # ASP.NET Core 8 controllers, middleware
├── tests/
│   ├── FinSight.Domain.Tests/          # xUnit + FluentAssertions
│   ├── FinSight.Application.Tests/     # NSubstitute mocks
│   ├── FinSight.Api.IntegrationTests/  # Testcontainers + real PostgreSQL
│   └── FinSight.ArchitectureTests/     # NetArchTest layer enforcement
├── helm/finsight/                # Kubernetes Helm chart
├── .github/workflows/            # GitHub Actions CI/CD
├── Dockerfile                    # Multi-stage, non-root, Alpine
└── docker-compose.yml            # API + PostgreSQL + Redis + Seq
```

## 🚀 Quick Start

### Prerequisites
- .NET 8 SDK
- Docker & Docker Compose

### Run locally
```bash
# 1. Copy and configure environment
cp .env.example .env
# Edit .env with your secrets

# 2. Start all services
docker compose up -d

# 3. Apply migrations
dotnet ef database update --project src/FinSight.Infrastructure --startup-project src/FinSight.Api

# 4. Browse the API
open http://localhost:8080/scalar/v1
```

## 🔑 Key Design Decisions

| Decision | Choice | Reason |
|----------|--------|--------|
| CQRS | MediatR 12.4.1 (last MIT) | Still valid, battle-tested |
| Mapping | Manual (records) | No AutoMapper license risk |
| Testing mocks | NSubstitute | Moq privacy incident in 2023 |
| DB | PostgreSQL | Zero cost, Stripe/Robinhood choice |
| AI | Microsoft.Extensions.AI | Model-agnostic, swap OpenAI ↔ local |
| Auth | Custom JWT + rotation | Full control, fintech compliance |
| Caching | StackExchange.Redis | Distributed, HybridCache-ready |
| API docs | Scalar (not Swagger) | Swashbuckle dropped from .NET 9+ |

## 🔐 Security Features

- **JWT access tokens** with 5-minute expiry (short for fintech)
- **Refresh token rotation** with token family theft detection
- **HTTP-only Secure cookies** for refresh tokens (XSS protection)
- **BOLA protection** on every endpoint (account ownership verified server-side)
- **Rate limiting** — 5 req/min on auth, 100 req/min on API
- **Security headers** — X-Content-Type-Options, X-Frame-Options, HSTS
- **Input validation** via FluentValidation pipeline behaviour

## 🤖 AI Features (Microsoft.Extensions.AI)

- **Spending insights** — Natural language summary of monthly spending
- **Anomaly detection** — ML.NET time-series spike detection
- **Budget advice** — AI-powered recommendations based on patterns
- **Model-agnostic** — Works with OpenAI, Azure OpenAI, or local Ollama

## 🧪 Testing Strategy

```bash
# Unit tests (Domain + Application)
dotnet test tests/FinSight.Domain.Tests
dotnet test tests/FinSight.Application.Tests

# Architecture tests (layer boundary enforcement)
dotnet test tests/FinSight.ArchitectureTests

# Integration tests (real PostgreSQL via Testcontainers)
dotnet test tests/FinSight.Api.IntegrationTests
```

## 📦 API Endpoints

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/api/v1/auth/register` | ❌ | Register new user |
| POST | `/api/v1/auth/login` | ❌ | Login + get tokens |
| GET | `/api/v1/accounts` | ✅ | List all accounts |
| POST | `/api/v1/accounts` | ✅ | Create account |
| GET | `/api/v1/transactions/account/{id}` | ✅ | Get transactions |
| POST | `/api/v1/transactions` | ✅ | Record transaction |
| GET | `/api/v1/insights/spending/{year}/{month}` | ✅ | AI spending insights |
| GET | `/health/live` | ❌ | Liveness probe |
| GET | `/health/ready` | ❌ | Readiness probe |

## 🌐 Environment Variables

```env
JWT_SECRET_KEY=minimum-64-character-secret-key-for-production
OPENAI_API_KEY=sk-...           # Optional — AI features disabled if absent
PLAID_CLIENT_ID=...             # Plaid Sandbox credentials
PLAID_SECRET=...
```
