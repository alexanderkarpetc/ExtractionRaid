#!/usr/bin/env bash
# Git clean filter: strips ProBuilder m_VersionIndex from Unity YAML files.
# Handles both direct fields (m_VersionIndex: 123) and prefab override blocks:
#   - target: {fileID: ..., guid: ...}
#     propertyPath: m_VersionIndex
#     value: 123

awk '
/propertyPath: m_VersionIndex/ {
    # Delete previous line (target:) stored in prev, skip this line, skip next (value:)
    prev = ""
    getline  # consume "value:" line
    next
}
{
    if (prev != "") print prev
    prev = $0
}
END {
    if (prev != "") print prev
}
' | sed 's/m_VersionIndex: [0-9]*/m_VersionIndex: 0/'
