# Uploading this project to GitHub

This guide was written by OpenAI Codex for the project owner.

## 1. Create an empty repository

1. Sign in to [GitHub](https://github.com/).
2. Select **New repository**.
3. Suggested repository name: `wechat-message-notifier`.
4. Choose **Public** or **Private**.
5. Do **not** add a README, `.gitignore`, or license during creation because these files already exist locally.
6. Select **Create repository**.

## 2. Configure Git identity

Open PowerShell in the project directory:

```powershell
Set-Location 'C:\Users\BIG DIO\Documents\Codex\2026-06-29\xiangmuban-wifi'
```

Configure the name and email recorded in this repository's commits:

```powershell
git config user.name "YOUR_GITHUB_NAME"
git config user.email "YOUR_GITHUB_EMAIL"
```

Using a GitHub-provided `noreply` email is recommended if you do not want to expose your personal email.

## 3. Create the first commit

```powershell
git init
git add .
git status
git commit -m "Initial release: WeChat Message Notifier v1.0.0"
git branch -M main
```

Review the output of `git status` before committing. Build executables, ZIP files, logs, and temporary test output are excluded by `.gitignore`.

## 4. Connect and push

Replace `YOUR_GITHUB_NAME` with your GitHub account name:

```powershell
git remote add origin https://github.com/YOUR_GITHUB_NAME/wechat-message-notifier.git
git push -u origin main
```

Git Credential Manager may open a browser for sign-in during the first push.

## 5. Publish the Windows build

The release archive is generated locally at:

```text
outputs\WeChatMessageNotifier-v1.0.0-Windows.zip
```

On GitHub:

1. Open the repository.
2. Select **Releases** → **Draft a new release**.
3. Create tag `v1.0.0`.
4. Use title `WeChat Message Notifier v1.0.0`.
5. Copy the `1.0.0` section from `CHANGELOG.md` into the release notes.
6. Attach `WeChatMessageNotifier-v1.0.0-Windows.zip`.
7. Select **Publish release**.

## 6. Check GitHub Actions

After pushing, open the repository's **Actions** tab. The `Windows build and test` workflow should compile the application, run its self-tests, and upload a Windows artifact.

The live WeChat integration test is intentionally not run in GitHub Actions because the hosted runner does not have a logged-in WeChat client.

## License reminder

No open-source license is currently included. Without a license, the project remains publicly visible if the repository is public, but reuse and redistribution are not automatically granted.

Add a license only after deciding what permissions you want to give other people.
