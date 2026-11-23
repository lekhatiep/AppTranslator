## Commit code
- Create a new branch following name stage_number, ex: stage_1
- Create message do not greater than 500 character, messages have to clearly, shortly to easy understand.
- Follow format commit message: "dd/mm/yyyy/hhmmss Your message", ex: "16/11/2026/192112 Giai đoạn 0: tạo solution"

## General Principles

Follow these principles when writing code:

- Prioritize readability and maintainability over cleverness.
- Keep solutions simple. Avoid unnecessary complexity.
- Write code that is easy for another developer to understand and review.
- Do not introduce abstractions unless they provide clear value.
- Do not over-engineer or implement features beyond the requirements.
- Follow existing project conventions and patterns.
- Do not modify unrelated code.
- Do not assume business requirements. Ask for clarification when requirements are ambiguous.

## C# Coding Standards

### Naming

- Use PascalCase for classes, methods, properties, and public members.
- Use camelCase for local variables and method parameters.
- Use meaningful names that describe their purpose.
- Avoid unclear abbreviations.
- Use descriptive names for boolean variables, such as `isValid`, `hasPermission`, and `canProcess`.

Readability Over Conciseness

Prefer explicit and easy-to-understand syntax over shorthand expressions.

Avoid advanced C# syntax when a simpler alternative is easier to read.

Avoid range operators (..), index-from-end operators (^), and complex pattern matching when they reduce readability.

Prefer traditional loops and conditions when they make the logic easier to follow.

Avoid deeply nested ternary operators.

Avoid combining multiple operations into a single LINQ statement if it makes the logic difficult to understand.

Do not use expression-bodied members for methods or properties when the implementation is not immediately obvious.

Avoid clever syntax that requires developers to understand multiple C# language features just to follow simple business logic.

Code should be understandable without requiring the reviewer to look up C# syntax.

### Methods

- Each method should have one clear responsibility.
- Keep methods short and easy to understand.
- Avoid deeply nested conditions.
- Extract complex logic into private methods when it improves readability.
- Avoid methods with too many parameters.
- Do not create helper methods for trivial one-line operations without a good reason.

### Clean Code

- Avoid duplicated code when it represents the same business logic.
- Avoid unnecessary comments that simply repeat what the code does.
- Add comments when business rules or non-obvious decisions need explanation.
- Avoid magic numbers and magic strings.
- Prefer early returns to deeply nested if/else statements when appropriate.
- Avoid unnecessary LINQ chains that make logic difficult to understand.
- Do not use overly complicated expressions when simpler code is possible.

## ASP.NET Core Guidelines

### Architecture

- Follow the existing project architecture.
- Keep responsibilities separated between Application,Business, and Domain when these layers exist.
- Do not introduce additional architectural layers without a clear requirement.

### Dependency Injection

- Use constructor injection.
- Depend on interfaces when appropriate.
- Register dependencies in the existing DI configuration.
- Avoid service locator patterns.

### Async Programming

- Use async/await for asynchronous I/O operations.
- Do not use `.Result` or `.Wait()` on asynchronous operations.
- Use `CancellationToken` where appropriate.
- Avoid unnecessary async methods when no asynchronous operation exists.

### Error Handling

- Do not silently swallow exceptions.
- Use try/catch only when the application can handle or add meaningful context to the error.
- Avoid unnecessary try/catch blocks.
- Return appropriate errors for expected business failures.
- Do not expose internal exception details or stack traces to API clients.

### Configuration and Security
- Never hardcode API keys, passwords, or secrets.
- Use configuration and environment variables for sensitive settings.
- Do not commit secrets to Git.
- Validate external input.
- Do not log passwords, tokens, or sensitive information.

## Database Guidelines

- Follow the existing database access approach.
- Use Entity Framework Core conventions when EF Core is used.
- Avoid unnecessary database queries.
- Be aware of N+1 query problems.
- Use AsNoTracking() for read-only queries when appropriate.
- Do not modify database schema without an explicit requirement.
- Keep database operations and business logic separated.

## Testing Guidelines

- Write unit tests for important business logic.
- Cover normal cases, edge cases, and failure cases.
- Use descriptive test names that explain the expected behavior.
- Keep tests independent and deterministic.
- Do not modify existing tests just to make new code pass.
- Run relevant tests after implementation.
- Never claim tests pass without actually running them.

## Code Review Requirements

When reporting completed work, always provide:

- **Files changed:** List modified and created files.
- **Implementation:** Explain the main logic and business flow.
- **Design decisions:** Explain important technical decisions.
- **Testing:** List tests executed and their results.
- **Potential issues:** Mention known limitations or concerns.

## Testing Guidelines

- Write unit tests for important business logic.
- Cover normal cases, edge cases, and failure cases.
- Use descriptive test names that explain the expected behavior.
- Keep tests independent and deterministic.
- Do not modify existing tests just to make new code pass.
- Run relevant tests after implementation.
- Never claim tests pass without actually running them.



## Implementation Rules
- Implementation Rules
- Before writing code:
1. Understand the existing project structure.

2. Read relevant existing code.

3. Understand the business requirements.

4. Identify which files need to change.

5. Explain the proposed approach for non-trivial tasks.

- During implementation:

1. Implement only what the requirements specify.

2. Prefer small and focused changes.

3. Reuse existing code when appropriate.

4. Avoid unnecessary refactoring.

5. Do not change public interfaces without a clear reason.

- After implementation:
- Review the changes for unnecessary complexity.
- Check for unrelated modifications.
- Explain what was changed and why.
## Code Review Requirements

When reporting completed work, always provide:

- **Files changed:** List modified and created files.
- **Implementation:** Explain the main logic and business flow.
- **Design decisions:** Explain important technical decisions.
- **Testing:** List tests executed and their results.
- **Potential issues:** Mention known limitations or concerns.

- Keep explanations concise and understandable.

## Important Rules

- Do not optimize prematurely.
- Do not add unnecessary design patterns.
- Do not create abstractions for hypothetical future requirements.
- Do not rewrite working code without a good reason.
- Do not hide complex business logic behind excessive helper methods.
- Prefer explicit and readable code over clever or compact code.
- If there are multiple possible solutions, prefer the simplest solution that satisfies the requirements.