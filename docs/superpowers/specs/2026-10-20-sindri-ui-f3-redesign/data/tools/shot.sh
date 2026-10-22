#!/bin/bash
# usage: shot.sh <page.html?query> <out.png> [width] [height]   (headless Chrome; the prototype fetches Google Fonts, blocked fonts fall back)
cd "$(dirname "$0")/../../screens" || exit 1
google-chrome --headless=new --no-sandbox --disable-gpu --hide-scrollbars --window-size=${3:-1440},${4:-900} --virtual-time-budget=3000 --screenshot="$2" "file://$PWD/$1" >/dev/null 2>&1
