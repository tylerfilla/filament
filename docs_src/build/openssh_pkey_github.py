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

import sys
from utils import ArgParseImpl

KEY_MAP = {
  '_0': '/',
  '_1': '+',
  '_2': '-',
  '_3': '=',
  '_4': '\n',
}

KEY_REV_MAP = {KEY_MAP[i] : i for i in KEY_MAP.keys()}

def _forward(secret):
  i = 0
  out = ""
  while i < len(secret):
    c = secret[i]
    if c == '_':
      c = secret[i:i+2]
      out += KEY_MAP[c]
      i += 2
    else:
      out += c
      i += 1
  return out

def _reverse(secret):
  for k, v in KEY_REV_MAP:
    secret = secret.replace(k, v)
  return secret

if __name__ == "__main__":
  parser = ArgParseImpl()
  parser.add_argument(
    '--reverse',
    action="store_true",
    help='Converts from OpenSSH key to github secret format'
  )
  arguments = sys.argv[1:]
  args, _ = parser.parse_known_args(arguments[0:len(arguments)-1])
  if args.reverse:
    print(_reverse(
