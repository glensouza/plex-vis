# Copilot Instructions

## Project Overview

This repository contains a .NET 10.0 Blazor Server application.

**Key Technologies:**
- .NET 10.0 / Blazor Server / C#
- .NET Aspire for local development orchestration with OpenTelemetry

## Deployment Architecture

**Development (Local):**
- .NET Aspire (recommended - integrated observability)
- Local .NET (direct development)

**Production:**
- Ubuntu 22.04 LTS (VM, bare metal, or cloud instance)
- No Docker - native installation
- Automated deployments via GitHub Actions (optional)

## Code of Conduct

All contributions must adhere to our [Code of Conduct](../CODE_OF_CONDUCT.md). Please be respectful, inclusive, and professional in all interactions.

## Contribution Guidelines

When contributing to this repository, please follow the guidelines in [CONTRIBUTING.md](../CONTRIBUTING.md).

### Git Commit Messages

- Use the present tense ("Add feature" not "Added feature")
- Use the imperative mood ("Move cursor to..." not "Moves cursor to...")
- Limit the first line to 72 characters or less
- Reference issues and pull requests liberally after the first line
- Consider starting the commit message with an applicable emoji:
  - 🎨 `:art:` when improving the format/structure of the code
  - 🐛 `:bug:` when fixing a bug
  - ✨ `:sparkles:` when introducing new features
  - 📝 `:memo:` when writing docs
  - 🚀 `:rocket:` when improving performance
  - ✅ `:white_check_mark:` when adding tests
  - 🔒 `:lock:` when dealing with security

## Code Style

**C# Specific (project hard rules):**
- **Always use explicit types; never use `var`.** This is a hard requirement.
- **Always declare explicit accessibility modifiers** on types and members (e.g., `public`, `internal`, `private`).
- **Async methods must end with the `Async` suffix.**
- Prefer **file-scoped namespaces** for new files.
- Prefer **using declarations** (`using var stream = ...;`) or `using` statements to ensure proper disposal of `IDisposable` objects.
- **Order using directives**: `System.*`, `Microsoft.*`, third-party, then project namespaces. Remove unused usings.
- Use primary constructors whenever possible.
- **No fields should start with underscore.** Use camelCase for private fields (e.g., `logger`, `settings`). **Always access private instance fields with `this.` prefix** (e.g., `this.logger`, `this.settings`). Only exception are those parameters from primary constructors.
- **Do not use `dynamic`.** Use explicit types or appropriate interfaces instead.
- **Avoid `object` type.** Use explicit types or appropriate interfaces instead if possible.
- **Avoid unnecessary line wrapping in simple expressions.** Keep simple statements on a single line, but wrap long or complex logic to maintain readability. Adhere to a maximum line length of 900 characters unless an exception is required for clarity.
- **Require XML documentation** (`/// <summary>`) for public types and public members. Keep comments concise.
- Use expression-bodied members only for simple one-line properties or methods; prefer full bodies for complex logic.
- Keep methods focused and single-purpose. Favor small, testable methods.
- Dispose `IDisposable` objects properly. Prefer `using`/`using var` or implement `IAsyncDisposable` when appropriate.
- Avoid blocking on async code. Use `async`/`await` throughout; do not call `.Result` or `.GetAwaiter().GetResult()` on tasks.
- Follow .editorconfig rules strictly for formatting (indentation, spacing, naming, line length).

**General:**
- Write clear, readable code with meaningful variable and function names.
- Comment complex logic, not obvious code.
- Follow existing code patterns and conventions in the project.
- Keep functions small and focused on a single task.
- Write self-documenting code where possible.
- Use LINQ for collections where appropriate.

## Documentation

**Documentation Requirements:**
- Update markdown files when changing functionality
- Document new features with clear examples
- Keep documentation in sync with code changes
- Use clear, concise language
- Ensure documentation is accessible for Pathfinders (10-15 years old) and leaders

## Security

**Critical Requirements:**
- Never commit sensitive information (API keys, passwords, personal data)
- Follow principle of least privilege (e.g., github-runner user NOT in sudo group)
- Use restrictive file patterns in sudoers (e.g., `[0-9a-f]*` for SHA hashes, not `*`)
- Set proper file permissions (600 for config files with secrets)
- Report security vulnerabilities privately via [SECURITY.md](../SECURITY.md)

**Production Security:**
- All services run as non-root users
- PostgreSQL listens only on localhost
- Nginx security headers configured
- Firewall with default-deny policy
- SSL/TLS with Let's Encrypt
- Automated deployments use restricted sudo via sudoers file

## Project-Specific Guidelines

### Application Architecture

### Performance

- Use async/await for I/O operations

**Documentation Changes:**
- Verify all internal links work
- Check markdown formatting
- Ensure consistency with related docs

## Deployment Paths

**Local Development:**
1. Aspire: `dotnet run --project PathfinderPhotography.AppHost` (automatic SigNoz)
2. Local .NET: `dotnet run` (manual PostgreSQL setup)

**Production Deployment:**
1. Manual: Follow [DEPLOY.md](../DEPLOY.md) step-by-step
2. Automated: Set up GitHub Actions runner on server (section 7 of deployment guide)
   - Every push to `main` triggers automated deployment
   - Includes backup, health checks, and automatic rollback

## Common Tasks

**Add a new EF migration:**
```bash
dotnet ef migrations add MeaningfulName
dotnet ef database update
```

**Test production build locally:**
```bash
dotnet publish -c Release -o ./publish
cd ./publish
ASPNETCORE_ENVIRONMENT=Production dotnet PathfinderPhotography.dll
```

**View logs in production:**
```bash
sudo journalctl -u pathfinder-photography -f
```

## Licensing

- All contributions will be licensed under the MIT License
- Ensure any third-party content or images used have appropriate permissions
- Provide attribution for external resources

## Questions and Support

If you have questions or need help:
- Check existing issues and discussions
- Review [SETUP.md](../SETUP.md) for development setup
- Review [DEPLOY.md](../DEPLOY.md) for production
- Open an issue with your question
- Reach out to the project maintainers

Thank you for contributing to this educational project! 🎉

---

## Guidance for Copilot when writing documentation

Purpose: provide explicit, actionable rules so Copilot produces consistent, well-formatted, and audience-appropriate Markdown documentation for this project.

- Prefer Markdown native elements; avoid HTML unless necessary.
- Use headings to show document structure. Start with a single H1 and use H2/H3 for sub-sections.
- Keep paragraphs short (1-3 sentences). Use bulleted or numbered lists for steps and enumerations.
- Use backticks for inline code, file names, directories, function names, classes, and commands (e.g., `SETUP.md`, `dotnet run`).
- Use fenced code blocks for commands and code examples. Specify language for syntax highlighting (e.g., ```bash, ```csharp).
- When documenting commands, show the full command and a short explanation on the next line.
- Provide examples and expected outputs for commands where helpful.
- Ensure all repository-relative links use correct relative paths (e.g., `[SETUP.md](../SETUP.md)`). Verify links exist before referencing them.
- Use accessible language: prefer simple sentences, define jargon, and include a short "Who this is for" note when the audience may vary.
- For content aimed at Pathfinders (10-15 years old), include brief explanations for why steps are done and actionable next steps.
- When writing step-by-step guides, use numbered lists and include prerequisite checks at the top.
- Keep list items concise; break complex steps into sub-steps.
- Add a "Troubleshooting" section for common errors and how to diagnose them (include exact error text and suggested checks).
- Add a "Verification" or "How to test" section that shows how to confirm the step worked.
- For configuration examples, include realistic default values but never include secrets or real credentials. Add a placeholder pattern: `<YOUR_VALUE_HERE>`.
- Use consistent tense and voice (imperative for instructions).
- Keep line length reasonable (~80-100 chars) to help readability in editors.
- Use emojis sparingly and only when they add clarity (e.g., checklists).
- When adding or updating docs, update the `DEPLOYMENT_CHECKLIST.md` or related index files if applicable.

Checklist for generated docs ( use this as a final-pass template):
1. Title and short description (one sentence)
2. Audience and prerequisites
3. Step-by-step instructions with code blocks and examples
4. Verification steps and expected results
5. Troubleshooting guidance for common failures
6. Links to related docs and references
7. No secrets included
8. Short summary or next steps

Examples (format):

Title: How to run the app locally

Short description: Run the Blazor Server app locally for development.

Prerequisites:
1. .NET 10 SDK installed

Steps:
1. Restore packages:

```bash
dotnet restore
```

2. Run the app:

```bash
dotnet run --project PathfinderPhotography.AppHost
```

Verification:
- Open `https://localhost:5001` in your browser and verify the home page loads.

Troubleshooting:
- If port conflict occurs, run `dotnet run --urls "https://localhost:5002"` and retry.

Notes for Copilot authors:
- When in doubt, ask for clarification instead of guessing architecture or sensitive values.
- Keep suggestions short and focused; prefer linking to existing docs in the repo over creating new ones.
- When creating or editing docs, ensure changes follow existing naming and style patterns in this repo.

---