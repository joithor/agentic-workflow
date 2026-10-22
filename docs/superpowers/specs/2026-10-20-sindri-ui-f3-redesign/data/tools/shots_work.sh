#!/bin/bash
cd "$(dirname "$0")" || exit 1
O=$(cd ../../screens && pwd)
./shot.sh "work.html?open=ITM-01JN2" $O/work-1440.png 1440 900
./shot.sh "work.html?open=ITM-01JN2" $O/work-900.png 900 700
./shot.sh "work.html" $O/work-list-1440.png
./shot.sh "work.html?state=loading" $O/work-loading-1440.png
./shot.sh "work.html?state=empty" $O/work-empty-1440.png
./shot.sh "work.html?state=stalled" $O/work-stalled-1440.png
./shot.sh "work.html?state=rebuilding" $O/work-rebuilding-1440.png
./shot.sh "work.html?state=down" $O/work-down-1440.png
./shot.sh "work.html?dialog=create&error=title" $O/work-create-error-1440.png
./shot.sh "work.html?open=ITM-01K6G&closed=1" $O/work-closed-1440.png
