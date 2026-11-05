#!/usr/bin/env bash

set -euo pipefail

# Cria e envia uma nova tag incrementada a partir da última tag existente.
# Suporta padrões:
# - 1, 2, 3...               -> incrementa inteiro
# - v1, v2, v3...            -> incrementa inteiro com prefixo v
# - 1.2.3 / v1.2.3           -> incrementa patch (1.2.4 / v1.2.4)

if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
	echo "Erro: execute este script dentro de um repositório Git." >&2
	exit 1
fi

remote_name="origin"

if ! git remote get-url "$remote_name" >/dev/null 2>&1; then
	echo "Erro: remoto '$remote_name' não encontrado." >&2
	exit 1
fi

# Atualiza referências de tags do remoto para evitar colisões locais.
git fetch --tags "$remote_name" >/dev/null 2>&1 || true

last_tag="$(git tag --sort=-v:refname | head -n 1)"
new_tag=""

if [[ -z "${last_tag}" ]]; then
	new_tag="v1"
else
	if [[ "${last_tag}" =~ ^v?([0-9]+)\.([0-9]+)\.([0-9]+)$ ]]; then
		prefix=""
		[[ "${last_tag}" == v* ]] && prefix="v"

		major="${BASH_REMATCH[1]}"
		minor="${BASH_REMATCH[2]}"
		patch="${BASH_REMATCH[3]}"

		patch=$((patch + 1))
		new_tag="${prefix}${major}.${minor}.${patch}"
	elif [[ "${last_tag}" =~ ^v([0-9]+)$ ]]; then
		number="${BASH_REMATCH[1]}"
		new_tag="v$((number + 1))"
	elif [[ "${last_tag}" =~ ^([0-9]+)$ ]]; then
		number="${BASH_REMATCH[1]}"
		new_tag="$((number + 1))"
	else
		echo "Erro: padrão de tag não suportado na última tag: '${last_tag}'." >&2
		echo "Use tags no formato inteiro, vN, N.N.N ou vN.N.N." >&2
		exit 1
	fi
fi

if git rev-parse -q --verify "refs/tags/${new_tag}" >/dev/null; then
	echo "Erro: a tag '${new_tag}' já existe localmente." >&2
	exit 1
fi

echo "Última tag: ${last_tag:-nenhuma}"
echo "Nova tag: ${new_tag}"

git tag "${new_tag}"
git push "$remote_name" "${new_tag}"

echo "Tag '${new_tag}' criada e enviada para '${remote_name}'."
