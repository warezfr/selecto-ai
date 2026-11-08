# Microsoft Store listing: English (en-US)

Text to paste into Partner Center (Store listing > en-US). Every claim comes from the documented behaviour of the app (root README); do not add anything that cannot be verified.

## Product name
Selecto AI

## Short description (100 characters max)
An action bar that appears when you select text, in any application.

## Description
Select text in any Windows application: a small floating bar appears right above it and offers the action you need, without switching windows.

What the bar does
• Search the selection with the search engine of your choice.
• Open a link, show a file or folder in File Explorer, write to an e-mail address: the action adapts to what you select.
• Cut, copy and paste, when the field is editable.
• Dictionary, translation and an AI assistant (summarize, explain, rephrase, correct): the answer appears in a small popup near the selection, with "Replace" and "Copy".
• Your own extensions: plain JavaScript files.

AI, your way
• Online actions (translation, dictionary, AI) are ready right after installation and can be turned off at any time in Settings > AI. Text is only sent when you trigger an action.
• Optional local AI: Selecto AI can install the Ollama engine and a model on your PC in one click, without administrator rights; the engine answers on a local address of your computer.
• Or your own provider: you enter your API key (OpenAI, Anthropic, Google, Mistral and others); it is encrypted with Windows (DPAPI) on your PC.

Made for Windows 11
• Fluent interface: light or dark theme and accent colour follow Windows.
• Easy to tune: global keyboard shortcut, applications and sites where the bar must not appear, bar size and position.
• Works on Windows 10 and 11, Intel/AMD (x64) and ARM64 processors.

Free and open source
Selecto AI is free, with no time limit and no account. Its code is public under the MIT License: https://github.com/warezfr/selecto-ai

Good to know
• Selecto AI reads the text you select in order to offer actions. It uses Windows accessibility (UI Automation) and, as a fallback, a temporary copy to the clipboard that it restores afterwards.
• When you use translation, dictionary or online AI, the selected text is sent to the service you chose. Details: the Privacy page of the website.
• Something wrong? "Report a problem" (tray icon menu or About page) sends your report straight to the publisher, with a screenshot if you want; logs are attached only if you tick the box, after previewing them.
• The app watches the mouse and keyboard to detect a selection (a global Windows hook): this is why it declares the "runFullTrust" capability.

## What's new in this version
Version 4.0.0: Selecto AI is now free and open source (MIT License). No trial and no key: every feature is available. The code is on GitHub: https://github.com/warezfr/selecto-ai

## Search terms (7 max)
text selection
action bar
translation
dictionary
AI assistant
productivity
shortcut

## Notes for certification (tester-only field)
Selecto AI uses a global low-level mouse/keyboard hook (runFullTrust) to detect text selection in other applications and show a floating action bar. No account is required. The app is free and open source (MIT); all features are enabled. To test: select text in any application (e.g. Notepad); the bar appears above the selection. Online actions (translation, dictionary, AI) are on by default and can be turned off in Settings > IA. Selected text is sent to the chosen services (MyMemory, Wiktionary, Wikipedia, or your AI model) only when you trigger an action.
