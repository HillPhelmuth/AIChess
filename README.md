# AIChess

AIChess is a sophisticated chess platform built with .NET and Blazor that allows users to play chess against various Large Language Models (LLMs), run AI vs. AI matches, and evaluate model performance.

![Chess Hero](AIChess/wwwroot/images/chess-play-image-chatgpt.png)

## Features

- **Play vs. AI**: Challenge a wide range of AI models including OpenAI (GPT-4, GPT-3.5), Google Gemini, and many others via OpenRouter.
- **AI vs. AI Matches**: Spectate matches between different AI models to see how they compare in tactical and strategic play.
- **Model Evaluation Engine**: A dedicated engine to run automated tournaments between models, generating detailed performance reports and win/loss statistics.
- **Interactive Chessboard**: A responsive and feature-rich board built with Radzen components, supporting move validation, SAN/FEN/PGN formats, and move history.
- **Stockfish Integration**: Leverages the Stockfish engine for "cheat" suggestions and as a baseline for AI performance.
- **Fine-tuning Data Generation**: Tools to generate structured chess data in JSONL format for fine-tuning LLMs on chess moves and reasoning.
- **Game Management**: Save and load games, undo moves, and view detailed game logs with AI reasoning.

## Project Structure

The solution is divided into several key projects:

- **[AIChess](AIChess/)**: The main Blazor Server web application containing the UI components and pages.
- **[AIChess.Core](AIChess.Core/)**: The heart of the application, containing game services, AI integration via Semantic Kernel, and core models.
- **[AIChess.ModelEvals](AIChess.ModelEvals/)**: A specialized library for running and managing AI model evaluations.
- **[ChessLibrary](ChessLibrary/)**: A robust chess logic library (forked from Gera Chess) for board management and rule enforcement.


## Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later.
- API Keys for the models you wish to use (OpenAI, Google AI, or OpenRouter).
- An Auth0 account for authentication.

### Configuration

1. Clone the repository.
2. Navigate to the `AIChess` project folder.
3. Update `appsettings.json` with your API keys and Auth0 configuration:

```json
{
  "OpenRouter": {
    "ApiKey": "YOUR_OPENROUTER_KEY"
  }
}
```

### Running the Application

From the root directory, run:

```bash
dotnet run --project AIChess/AIChess.csproj
```

The application will be available at `https://localhost:7254` (or the port specified in `launchSettings.json`).


## Data Generation

The project includes `ChessDataGenerator` to create training datasets for fine-tuning. It generates JSONL files containing board states, available moves, and the "best move" as determined by Stockfish or other high-performing models.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [Gera Chess Library](https://github.com/geras1mleo/chess) for the foundational chess logic.
