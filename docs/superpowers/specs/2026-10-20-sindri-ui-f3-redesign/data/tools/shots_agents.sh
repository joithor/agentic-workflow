#!/bin/bash
# the agents.html captures (PNG at 1440 and 900, plus one per state)
cd "$(dirname "$0")" || exit 1
O=$(cd ../../screens && pwd)
./shot.sh "agents.html" $O/agents-1440.png 1440 900
./shot.sh "agents.html" $O/agents-900.png 900 700
./shot.sh "agents.html?state=reading&closed=1" $O/agents-loading-1440.png
./shot.sh "agents.html?state=empty" $O/agents-empty-1440.png
./shot.sh "agents.html?state=problems&closed=1" $O/agents-problems-1440.png
./shot.sh "agents.html?state=stale&closed=1" $O/agents-stale-1440.png
./shot.sh "agents.html?state=unavailable" $O/agents-unavailable-1440.png
./shot.sh "agents.html?state=unreadable" $O/agents-unreadable-1440.png
./shot.sh "agents.html?state=nomatch" $O/agents-nomatch-1440.png
./shot.sh "agents.html?ended=1&open=claude-code/2c9ab417-8e03-41d2-b6f5-71a0c3d9e4b2" $O/agents-ended-1440.png
