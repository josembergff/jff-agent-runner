namespace Domain.Prompts;

public static class IssueImplementationPrompt
{
    public static string Build(int issueNumber) => $$"""
        Implement Issue #{{issueNumber}} of this repository.

        ### STAGE 1 — ANALYSIS

        1. Read the Issue using the GitHub CLI (`gh issue view {{issueNumber}}`).
        2. Analyze the requirements and acceptance criteria.
        3. Analyze the existing architecture, structure and patterns.
        4. Identify the files and components that need to change.
        5. If there are ambiguities or insufficient information, ask before implementing.

        ### STAGE 2 — IMPLEMENTATION

        1. Switch to the branch `agent/{{issueNumber}}`, creating it from `main` if it does not exist.
        2. Implement all the requirements of the Issue.
        3. Create or update the tests.
        4. Run the tests and validate the build.
        5. Fix any problems found.
        6. Review the changes before finishing.

        ### STAGE 3 — PULL REQUEST

        After completing the implementation and validating the tests:

        1. Commit the changes to the current branch.
        2. Push the branch `agent/{{issueNumber}}` to GitHub.
        3. Create a Pull Request targeting the `main` branch.
        4. Use the GitHub CLI (`gh pr create`).
        5. Include the Issue number and an objective description of the changes in the title.
        6. Link the Pull Request to the Issue using `Closes #{{issueNumber}}`.

        ### MANDATORY RULES

        - Work exclusively on the branch `agent/{{issueNumber}}`.
        - Never change or commit directly to `main`.
        - Never merge the Pull Request.
        - Do not invent requirements or behaviors.
        - Do not discard existing changes.
        - Do not modify files outside the scope without justification.
        - Do not push other branches.
        - You are authorized to push only the branch `agent/{{issueNumber}}` to create the requested Pull Request.
        - If there are ambiguities, stop and ask for clarification.
        - Do not report tests as passing without running them.
        - When finished, present a summary of the changes, the tests run and the Pull Request link.
        """;
}
