# Project Rules

## Default Git Workflow

For this repository, after completing code changes, follow this workflow by default unless the user explicitly says `不要提交` or `不要推送`.

1. Check the working tree and keep only changes related to the current task.
2. Do not revert or overwrite unrelated user changes.
3. Run the smallest relevant verification available, such as focused tests or a targeted build.
4. If there is no blocking error, run:
   - `git add .`
   - `git commit -m "<clear, short, accurate message for this task>"`
   - `git push`
5. If the branch has no upstream yet, create the upstream during the first push.
6. If push fails, keep the local commit and report the failure reason plus the next suggested action.
7. Do not auto-push before asking the user if the change includes:
   - secrets, tokens, certificates, or private data
   - obvious build outputs, large binaries, or temporary files that should not be committed
   - large unrelated edits or changes that may interfere with unfinished user refactors

## Reporting

After each completed task, report:

1. the commit message
2. the commit hash
3. whether push succeeded and which branch received it
