
# FinanceTracker Project Instructions

## Project purpose
FinanceTracker is a private household finance tracker. It will
consolidate financial information from user-uploaded
statements and provide an accurate view of assets,
liabilities, transactions, investments and net worth.

## Technology stack
- Backend: C#, ASP.NET Core and Entity Framework Core.
- Frontend: React and TypeScript.
- Database: Azure SQL Database.
- File storage: private Azure Blob Storage.
- Infrastructure: Azure Bicep.
- CI/CD: GitHub Actions with Azure OIDC federation.
- Source control: private GitHub repository.

## Architecture
- Use a modular monolith. Do not introduce microservices
  without an explicit architectural decision.
- Keep domain rules independent of infrastructure.
- Keep business use cases in the Application layer.
- Keep persistence and external service implementations
  in Infrastructure.
- Keep HTTP concerns in API and UI concerns in Web.
- Keep statement parsing and import workflows in Importing.
- Use dependency injection and explicit interfaces where
  they provide meaningful separation.
- Document significant architectural decisions.

## Financial correctness
- Never use floating-point types for monetary amounts.
- Use decimal types and suitable SQL decimal precision.
- Define currency and rounding rules explicitly.
- Preserve source statements and original transaction
  values where appropriate.
- Make imports idempotent and detect duplicate records.
- Never silently discard, overwrite or reinterpret
  financial data.
- Use synthetic financial data in development and tests.

## Security and privacy
- Treat all financial records and uploaded statements
  as sensitive.
- Never commit credentials, secrets or real statements.
- Keep uploaded files private and restrict access.
- Validate file types, sizes and contents.
- Enforce authentication and authorization on protected
  endpoints and resources.
- Do not log account numbers, balances, statement contents
  or other sensitive financial data.
- Use managed identity or workload federation where
  appropriate. Never hardcode secrets.

## Infrastructure as Code
- All Azure resources and configuration must be defined
  and version-controlled using Bicep.
- All deployments must run through reviewed automation.
- Do not deploy or modify Azure resources manually through
  the Azure Portal or ad hoc CLI commands.
- Use GitHub Actions and Azure OIDC federation.
- Separate development and production configuration.
- Validate templates and run deployment what-if before
  applying infrastructure changes.
- Require explicit approval before real deployments,
  destructive operations or changes that may incur costs.
- Do not run production deployments autonomously.

## Engineering standards
- Follow current supported .NET and Node.js LTS versions.
- Use nullable reference types and appropriate analyzers.
- Add automated tests for business rules and new behavior.
- Prefer small, maintainable changes over speculative
  abstractions.
- Update documentation when architecture or behavior changes.
- Never claim a test passed unless it was actually run.
- Never claim a deployment succeeded unless verified.

## AI agent workflow
For each task:
1. Read the relevant project instructions and documentation.
2. Inspect the existing implementation before changing it.
3. Identify requirements, assumptions and affected components.
4. Propose a concise implementation plan and test plan.
5. Ask for clarification when a material requirement is
   ambiguous; otherwise state reasonable assumptions.
6. Implement one bounded feature at a time.
7. Run relevant tests and fix failures.
8. Review the diff for security, correctness and scope.
9. Summarize changed files, test results, risks and
   remaining work.
10. Stop before live infrastructure changes or deployments
    until explicitly approved.

Do not silently change architectural decisions, weaken
security controls, skip tests or expand the task scope.
