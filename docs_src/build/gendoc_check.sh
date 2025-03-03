#!/usr/bin/bash

# Copyright (C) 2025 The Android Open Source Project
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#      http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

COMMIT_HASH=$1
GIT_SSH_PUB_KEY=$
DOCS_SRC_DIR='docs_src/'
DOCS_SRC_BUILD_DIR="${DOCS_SRC_DIR}/build"

if python3 ${DOCS_SRC_BUILD_DIR}/checks.py --do-and="source_edits,commit_docs_bypass" $COMMIT_HASH; then
    echo "Bypassed generating /docs due to bypass tag in commit message"
elif python3 ${DOCS_SRC_BUILD_DIR}/checks.py --do-or="source_edits,commit_docs_force" $COMMIT_HASH; then
    set -xe    
    bash ${DOCS_SRC_BUILD_DIR}/install_mdbook.sh
    python3 ${DOCS_SRC_BUILD_DIR}/run.py
    rm -rf docs/*
    mdbook build ${DOCS_SRC_DIR} --dest-dir=$(pwd)/docs

    # We need to commit the changes to the source tree
    git add docs/*
    git commit -m "docs: auto-generated update"
    git push origin main
else
    echo "No changes to documentation"
fi
