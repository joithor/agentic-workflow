# Performance lens

Read the diff for cost that grows with data or traffic. Do not flag micro-optimisations.

Look for:
- Queries inside loops, missing indexes for a new filter, unbounded result sets.
- Algorithms whose cost grows faster than the input on a realistic size.
- Repeated work that could be done once: reading a file, parsing, building a client.
- Blocking calls on a hot or latency-sensitive path; missing timeouts and cancellation.
- Memory that grows without bound: caches with no limit, buffering a whole stream, leaked handles.
- Work done on startup or on every request that could be deferred.

Severities:
- blocking: the change will not survive realistic load, or can exhaust memory or connections.
- issue: a clear scaling problem on a path that is used often.
- suggestion: an improvement that is worth doing but not needed to merge.
- nit: a micro-optimisation or style point.
