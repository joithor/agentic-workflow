### Opening a link

assumes: D3.a

The public route compares the link's expiry time with the server clock once per request. A link past its time answers
410 and the note is not read (AC-2).
