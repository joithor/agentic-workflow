import re,sys
txt=open(sys.argv[1]).read()
res={}
for m in re.finditer(r"error (RS001[67]): Symbol '(.*?)' is (?:not part of the declared public API|part of the declared API, but is either not public or could not be found) \(.*?\[(/[^\]]*?)/([^/\]]*)\.csproj\]",txt):
    code,sym,proj,name=m.groups()
    res.setdefault(proj,{'add':set(),'rem':set()})['add' if code=='RS0016' else 'rem'].add(sym)
for proj,d in res.items():
    p=proj+'/PublicAPI.Unshipped.txt'
    lines=open(p).read().split('\n')
    if lines and lines[-1]=='': lines.pop()
    keep=[l for l in lines if l not in d['rem']]
    removed=len(lines)-len(keep)
    for sym in sorted(d['add']):
        if sym in keep: continue
        i=next((k for k,l in enumerate(keep) if not l.startswith('#') and l>sym),len(keep))
        keep.insert(i,sym)
    open(p,'w').write('\n'.join(keep)+'\n')
    print(proj,'added',len(d['add']),'removed',removed,'rem-requested',len(d['rem']))
