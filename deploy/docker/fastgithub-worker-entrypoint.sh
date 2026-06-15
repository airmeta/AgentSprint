#!/usr/bin/env bash
set -euo pipefail

FASTGITHUB_PROXY="${FASTGITHUB_PROXY:-http://127.0.0.1:38457}"

cd /opt/fastgithub
./fastgithub >/var/log/fastgithub.log 2>&1 &

for _ in {1..30}; do
  if curl --silent --head --max-time 2 --proxy "$FASTGITHUB_PROXY" https://github.com >/dev/null 2>&1; then
    if [ -f /opt/fastgithub/cacert/fastgithub.cer ]; then
      cp /opt/fastgithub/cacert/fastgithub.cer /usr/local/share/ca-certificates/fastgithub.crt
      update-ca-certificates >/tmp/fastgithub-update-ca.log 2>&1 || cat /tmp/fastgithub-update-ca.log
    fi
    git config --global --unset-all http.sslbackend >/dev/null 2>&1 || true
    git config --global http.https://github.com.proxy "$FASTGITHUB_PROXY"
    git config --global http.https://www.github.com.proxy "$FASTGITHUB_PROXY"
    git config --global http.https://raw.githubusercontent.com.proxy "$FASTGITHUB_PROXY"
    git config --global http.https://gist.github.com.proxy "$FASTGITHUB_PROXY"
    git config --global http.https://api.github.com.proxy "$FASTGITHUB_PROXY"
    break
  fi
  if [ -f /opt/fastgithub/cacert/fastgithub.cer ] && [ ! -f /usr/local/share/ca-certificates/fastgithub.crt ]; then
    cp /opt/fastgithub/cacert/fastgithub.cer /usr/local/share/ca-certificates/fastgithub.crt
    update-ca-certificates >/tmp/fastgithub-update-ca.log 2>&1 || cat /tmp/fastgithub-update-ca.log
  fi
  sleep 1
done

git config --global --unset-all http.sslbackend >/dev/null 2>&1 || true
git config --global http.https://github.com.proxy "$FASTGITHUB_PROXY"
git config --global http.https://www.github.com.proxy "$FASTGITHUB_PROXY"
git config --global http.https://raw.githubusercontent.com.proxy "$FASTGITHUB_PROXY"
git config --global http.https://gist.github.com.proxy "$FASTGITHUB_PROXY"
git config --global http.https://api.github.com.proxy "$FASTGITHUB_PROXY"

if [ -z "${OPENAI_API_KEY:-}" ] && [ -n "${AgentSprint__ApiBaseUrl:-}" ] && [ -n "${AgentSprint__AgentToken:-}" ]; then
  runtime_config_url="${AgentSprint__ApiBaseUrl%/}/worker-runtime/config"
  runtime_config_json="$(curl --silent --show-error --max-time 10 \
    --header "Authorization: Bearer ${AgentSprint__AgentToken}" \
    "${runtime_config_url}" || true)"
  runtime_openai_api_key="$(printf '%s' "$runtime_config_json" | node -e 'let input="";
process.stdin.on("data", chunk => input += chunk);
process.stdin.on("end", () => {
  try {
    const payload = JSON.parse(input);
    const value = String(payload?.data?.openAiApiKey || "").trim();
    process.stdout.write(value);
  } catch {
    process.stdout.write("");
  }
});')"
  if [ -n "$runtime_openai_api_key" ]; then
    export OPENAI_API_KEY="$runtime_openai_api_key"
    echo "Worker entrypoint loaded OPENAI_API_KEY from runtime config."
  else
    echo "Worker entrypoint did not receive openAiApiKey from runtime config."
  fi
elif [ -n "${OPENAI_API_KEY:-}" ]; then
  echo "Worker entrypoint keeps existing container OPENAI_API_KEY."
else
  echo "Worker entrypoint cannot load OPENAI_API_KEY because AgentSprint API base URL or AgentToken is missing."
fi

cd /app
exec dotnet AgentSprint.Worker.dll
