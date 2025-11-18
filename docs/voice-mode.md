Voice mode

We are going to setup voice mode: https://voice-mode.readthedocs.io/en/latest/#features


1. Install uvx (a python installation manager)
```
powershell -ExecutionPolicy ByPass -c "irm https://astral.sh/uv/install.ps1 | iex"
```

2. Activate python 3.12 and install the voice-mode package.

```
# install python
uv python install 3.12

# Create a new virtual environment with Python 3.12
uv venv --python 3.12

# Activate it
.venv\Scripts\activate

# Install voice-mode (should work smoothly now)
uv pip install voice-mode

```

3. install ffmpeg dependency

```

winget install ffmpeg

```

4. restart terminal and test

```
   ffmpeg -version
   uvx --python 3.12 voice-mode
```


5. add mcp server in claude - use python 3.12

```

"voice-mode": {
  "command": "uvx",
  "args": [
    "--python", "3.12",
    "voice-mode"
  ],
  "env": {
    "OPENAI_API_KEY": "your-openai-key"
  }
}

```

Prompt: "lets have a voice conversation"

