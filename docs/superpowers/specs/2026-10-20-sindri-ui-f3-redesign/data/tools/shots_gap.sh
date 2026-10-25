#!/bin/bash
# the captures for system, jobs, recipes and home (PNG at 1440 and 900, plus one per state)
cd "$(dirname "$0")" || exit 1
O=$(cd ../../screens && pwd)
./shot.sh "system.html" $O/system-1440.png 1440 900
./shot.sh "system.html" $O/system-900.png 900 700
for s in degraded profile rebuilding stalled stale down mismatch unreadable connecting; do ./shot.sh "system.html?state=$s" $O/system-$s-1440.png; done
./shot.sh "system.html?tab=services" $O/system-services-1440.png
./shot.sh "jobs.html?open=JOB-01KCM3" $O/jobs-1440.png 1440 900
./shot.sh "jobs.html?open=JOB-01KCM3" $O/jobs-900.png 900 700
for s in reading empty norunning unreadable failed; do ./shot.sh "jobs.html?state=$s" $O/jobs-$s-1440.png; done
./shot.sh "jobs.html?open=JOB-01KCL9" $O/jobs-failed-job-1440.png
./shot.sh "recipes.html" $O/recipes-1440.png 1440 900
./shot.sh "recipes.html" $O/recipes-900.png 900 700
for s in reading empty nomatch stale unreadable; do ./shot.sh "recipes.html?state=$s&closed=1" $O/recipes-$s-1440.png; done
./shot.sh "recipes.html?open=workflow.pr-babysitter" $O/recipes-unapproved-1440.png
./shot.sh "home.html" $O/home-1440.png 1440 900
./shot.sh "home.html" $O/home-900.png 900 700
for s in firstrun quiet loading stale down; do ./shot.sh "home.html?state=$s" $O/home-$s-1440.png; done
