# uruk-client-dotnet
.Net client for Uruk

# What is the Uruk project?
The Uruk project provide an audit trail solution, including authentication, non-repudiation and integrity.

## Key concepts
### Security Event Tokens structure
The Uruk project use the Security Event Tokens [RFC8417](https://tools.ietf.org/html/rfc8417) as well for structure and delivery.
This kind of token ensure authentication, non-repudiation and integrity of the record itself.

### High resilience client
The client is highly resilient to failure. This imply: 
- Retry on network failure
- Keep a secure backup when retry is not possible
- Resume the flow on restart

# Why the name of `Uruk`?
Uruk was an [ancient city of Sumer](https://en.wikipedia.org/wiki/Uruk), during the Uruk period. 
At this period, [bulla](https://en.wikipedia.org/wiki/Bulla_(seal)) were used for tamper-proofing commercial and legal affairs.
