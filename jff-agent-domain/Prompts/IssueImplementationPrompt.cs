namespace Domain.Prompts;

public static class IssueImplementationPrompt
{
    public static string Build(int issueNumber) => $$"""
        Implement Issue #{{issueNumber}} of this repository.

        Before starting, read and follow the repository's `AGENT_INSTRUCTIONS.md` and `AGENTS.md`.
        These are mandatory instructions for this execution. Complete the implementation, validation,
        commit, push and Pull Request without asking for routine confirmation. Resolve ordinary
        ambiguity from the issue and repository context. Ask only if an essential requirement cannot
        be inferred or a real external blocker prevents progress; finish independent work first.

        ### STAGE 1 — ANALYSIS

        1. Read the Issue using the GitHub CLI with this exact command pattern, replacing `34` with `{{issueNumber}}`:
           ```bash
           gh issue view {{issueNumber}} --json title,body,labels --jq '{title, body, labels}'
           ```
           Use the returned title, body and labels as the issue information. Do not use a different `gh issue view` output format.
        2. Analyze the requirements and acceptance criteria.
        3. Analyze the existing architecture, structure and patterns.
        4. Identify the files and components that need to change.
        5. Resolve routine ambiguity from the acceptance criteria and repository conventions. Ask only
           if an essential requirement cannot reasonably be inferred; do not use questions to defer
           implementation or the required Pull Request workflow.

        ### STAGE 2 — IMPLEMENTATION

        1. Switch to the branch `agent/{{issueNumber}}`, creating it from `main` if it does not exist.
        2. Implement all the requirements of the Issue.
        3. Create or update the tests.
        4. Run the tests and validate the build.
        5. Fix any problems found.
        6. Review the changes before finishing.

        ### STAGE 3 — PULL REQUEST

        After completing the implementation and running the relevant available checks:

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
        - Do not stop for routine confirmation. If an essential requirement cannot reasonably be
          inferred or an external blocker prevents progress, complete independent work first and
          report the precise blocker and action needed.
        - Do not report tests as passing without running them.
        - When finished, present a summary of the changes, the tests run and the Pull Request link.
        """;
}
