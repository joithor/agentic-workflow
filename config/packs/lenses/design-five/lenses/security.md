# Security and threat-model lens

Read the design as someone who wants to misuse it. Work through the plan before any code exists.

Look for:
- Trust boundaries: where input enters, who may send it, and what is assumed about it.
- Authentication and authorisation: who may do each thing, checked where, and what happens when the check is skipped.
- Data exposure: what is stored, logged, returned or sent to another system, and whether secrets or personal data could reach any of it.
- Injection and unsafe handling of input: queries, commands, paths, templates, deserialisation, prompts to a model.
- STRIDE, briefly: spoofing, tampering, repudiation, information disclosure, denial of service, elevation of privilege.
- Dependencies and supply chain: new packages, services or permissions the design adds.
- Abuse of the feature as designed: rate, size, replay and enumeration.

Severities:
- CRITICAL: an unauthenticated or unauthorised path to data or actions, an exposed secret, or remote code execution.
- HIGH: a missing check on a sensitive action, sensitive data in a log or response, or an injection path.
- MEDIUM: a missing limit, an over-broad permission, or a weakness that needs another flaw to be used.
- LOW: hardening that would be good practice.
