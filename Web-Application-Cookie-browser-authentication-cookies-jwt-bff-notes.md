# Browser Authentication Architecture: Cookies, Sessions, JWTs and BFF

> **Purpose:** A practical reference for understanding browser
> authentication options and choosing a secure architecture for Angular
> SPAs, .NET APIs, Azure API Management and microservices.
>
> **Core recommendation:** For many enterprise browser applications, a
> Backend for Frontend (BFF) with an opaque, secure session cookie and
> server-side OAuth token handling is a strong default. It is not
> universally mandatory; choose based on the application's threat model,
> clients and operational requirements.

------------------------------------------------------------------------

## 1. The mental model: four different concepts

Do not treat cookies, sessions and JWTs as competing alternatives. They
describe different things.

  -----------------------------------------------------------------------------
  Concept                 What it is                   What it does
  ----------------------- ---------------------------- ------------------------
  **Cookie**              An HTTP mechanism for        Carries a session
                          storing and sending a small  identifier or, in some
                          value between a browser and  designs, a token
                          a server                     

  **Session**             Authentication/application   Lets the server remember
                          state associated with a user that a user has
                                                       authenticated

  **JWT**                 A token format containing    Can carry
                          claims, commonly signed      identity/authorization
                                                       information that a
                                                       recipient validates

  **BFF**                 A backend component          Keeps OAuth tokens on
                          dedicated to serving a       the server side and
                          particular frontend          provides a
                                                       browser-facing session
  -----------------------------------------------------------------------------

A cookie is not necessarily a session, and a session does not have to
use JWT. A JWT can be sent in an `Authorization` header or stored in a
cookie. A cookie containing a JWT is still a cookie-based transport
design.

### 1.1 Three common patterns

**A. Opaque session cookie**

``` text
Browser                         Application
   |                                 |
   | Cookie: session_id=random       |
   |-------------------------------->|
                                     |
                              Look up session
                                     |
                               Redis / DB
                                     |
                              User is identified
```

The cookie contains an unpredictable session identifier. The server
looks up the corresponding session state.

**B. Bearer access token (often a JWT)**

``` text
Browser / Client                 API
   |                              |
   | Authorization: Bearer <token>|
   |----------------------------->|
                                  |
                         Validate token
                    (signature, issuer, audience,
                     expiry and other claims)
                                  |
                         Apply authorization
```

The API can validate a self-contained signed JWT locally, without
looking up a session on every request, if the design does not require an
additional state check.

**C. JWT carried in a cookie**

``` text
Browser                         Application
   |                                 |
   | Cookie: access_token=<JWT>      |
   |-------------------------------->|
                                     |
                               Validate JWT
                                     |
                              Apply authorization
```

This uses cookie transport, but it is not automatically a traditional
server-side session. Because browsers attach cookies automatically, CSRF
must be considered.

------------------------------------------------------------------------

## 2. Traditional server-side session authentication

### 2.1 Authentication flow

1.  The user submits login credentials or completes an identity-provider
    sign-in.
2.  The application authenticates the user.
3.  The server creates a session and stores its state.
4.  The server sends the browser a cookie containing an opaque session
    identifier.
5.  The browser stores the cookie and sends it with applicable requests.
6.  The server uses the identifier to retrieve the session and
    authenticate the request.

``` text
User
 |
 | Sign in
 v
Application / Identity Provider
 |
 | Authentication succeeds
 v
Application creates session
 |
 | Store session state
 v
Session Store (in-memory / Redis / database)
 |
 | Set-Cookie: session_id=...
 v
Browser
 |
 | Cookie automatically sent on matching requests
 v
Application validates session
```

### 2.2 Does traditional session authentication scale?

A common challenge arises when each application instance stores sessions
only in its own memory:

-   A subsequent request may reach a different instance.
-   That instance may not know about the original instance's session.
-   Sticky sessions can reduce this issue but introduce routing and
    availability trade-offs.

Common solutions include:

-   A distributed session store such as Redis.
-   A shared database-backed session store.
-   Stateless authentication tokens, where appropriate.
-   Session affinity, where justified by the deployment design.

**Correction to remember:** Multiple servers do not inherently make
cookie/session authentication unsuitable. A shared session store is a
standard solution and may be preferable when immediate session
revocation and centralized session control matter.

### 2.3 Advantages and trade-offs

**Advantages** - Straightforward logout and session invalidation. -
Server controls session lifetime and state. - Session data can be
changed without issuing a new token. - Opaque session identifiers reveal
little about the user.

**Trade-offs** - Requires session lifecycle management. - A shared
session store adds infrastructure and operational considerations. -
Session lookup adds a dependency to request processing. - The session
store must be secured, monitored and made highly available.

------------------------------------------------------------------------

## 3. JWT and stateless access-token authentication

JWT stands for **JSON Web Token**. It is a token format, not an
authentication protocol by itself. OAuth 2.0 and OpenID Connect define
flows and roles; JWT is one possible token format used within those
systems.

### 3.1 Typical signed JWT structure

A compact signed JWT commonly has three Base64URL-encoded parts:

``` text
header.payload.signature
```

-   **Header:** token type and signing algorithm information.
-   **Payload:** claims such as issuer, subject, audience and expiry.
-   **Signature:** allows the recipient to verify integrity and the
    trusted issuer/signing key.

A signed JWT is generally **not encrypted**. Its claims can usually be
decoded by anyone who obtains the token. Do not put passwords, secrets
or sensitive personal information in an ordinary signed JWT.

### 3.2 What does stateless mean?

A resource API can validate a signed access token using trusted signing
keys and token claims without retrieving a user session for every
request.

Typical validation includes:

-   Signature and permitted algorithm.
-   Trusted issuer (`iss`).
-   Intended audience (`aud`).
-   Expiration (`exp`).
-   Not-before (`nbf`), when present.
-   Required scopes, roles and other authorization claims.

**Important:** Signature validation establishes that the token has not
been modified and was signed by a trusted key. It does not, by itself,
prove that the user is authorized to perform every requested operation.

### 3.3 Does JWT eliminate server-side state?

No. JWT-based access-token validation can be stateless, but an overall
authentication system may still keep state for:

-   Refresh-token rotation and revocation.
-   User sessions and device sessions.
-   Logout and account-disablement handling.
-   Token denylisting or revocation checks.
-   Consent, risk and security policies.

### 3.4 JWT advantages and trade-offs

**Advantages** - APIs can validate signed tokens locally. - Useful
across independently deployed services. - Claims can carry scopes, roles
and other authorization context. - Can reduce per-request session-store
lookups.

**Trade-offs** - A stolen bearer token can be used until it expires or
is otherwise rejected. - Revocation is harder if APIs validate tokens
entirely offline. - Token size can grow with claims. - Key rotation and
validation configuration must be managed correctly. - Claims can become
stale before token expiry.

**Key point:** JWT does not automatically solve distributed
authentication, and it is not automatically more secure than an opaque
session.

------------------------------------------------------------------------

## 4. Browser token-storage approaches

The key question is: **Can JavaScript running in the browser read the
credential?** Also consider whether the browser automatically attaches
the credential to requests.

### 4.1 Option 1 --- `localStorage`

``` javascript
localStorage.setItem("access_token", accessToken);
```

**Advantages** - Simple to implement. - Survives page refreshes and
browser restarts until removed or expired. - Application JavaScript can
read it and attach it to API requests.

**Risks** - JavaScript can read the token. - An XSS vulnerability can
allow malicious scripts to steal and exfiltrate it. - The application
must implement token renewal, expiry and cleanup.

**Assessment:** Convenient, but generally not the preferred place for
sensitive OAuth tokens when a BFF is practical.

### 4.2 Option 2 --- In-memory storage

``` javascript
let accessToken = null;
```

**Advantages** - Not automatically persisted across browser restarts. -
Avoids leaving the token in persistent browser storage. - Token lifetime
in the browser can be limited.

**Risks and limitations** - Malicious JavaScript executing in the
application can still access it. - A full page refresh clears ordinary
in-memory state. - The application needs a renewal or re-authentication
strategy. - Multi-tab coordination and token lifecycle can add
complexity.

**Assessment:** Reduces persistent exposure compared with local storage,
but it is not XSS-proof.

### 4.3 Option 3 --- Cookie carrying a token

The browser can send a cookie automatically. A cookie may contain a JWT
or another credential.

Recommended cookie attributes for a typical HTTPS browser application
should be assessed as follows:

  -----------------------------------------------------------------------
  Attribute                           Purpose
  ----------------------------------- -----------------------------------
  `HttpOnly`                          Prevents JavaScript from reading
                                      the cookie

  `Secure`                            Restricts cookie transmission to
                                      secure HTTPS connections

  `SameSite`                          Controls when cookies are sent in
                                      cross-site contexts and helps
                                      mitigate CSRF

  `Path`                              Restricts the URL paths for which
                                      the browser sends the cookie

  `Domain`                            Controls which hosts receive the
                                      cookie; avoid unnecessarily broad
                                      domain scope
  -----------------------------------------------------------------------

Example (illustrative; choose `SameSite` according to the sign-in and
deployment flow):

``` http
Set-Cookie: access_token=<token>;
            HttpOnly;
            Secure;
            SameSite=Lax;
            Path=/
```

**Advantages** - `HttpOnly` prevents ordinary JavaScript access to the
cookie value. - Browser handles cookie transmission. - `Secure` protects
against sending the cookie over plain HTTP.

**Risks and limitations** - Cookies are automatically attached to
matching requests, creating CSRF considerations. - `HttpOnly` does not
stop XSS from making authenticated requests through the victim's
browser. - A token stored in a cookie still needs expiry, renewal and
revocation design. - Cookie size and domain/path scope need
consideration.

**Assessment:** A cookie can protect a token from direct JavaScript
reading, but a browser-facing BFF is often a better way to keep OAuth
tokens out of browser code altogether.

### 4.4 Option 4 --- BFF with an opaque session cookie

This is often the preferred pattern for enterprise browser applications.

The browser receives an opaque session cookie. OAuth access and refresh
tokens are held and managed by the BFF/server side, not exposed to
browser JavaScript.

``` text
Angular SPA
    |
    | Opaque session cookie
    | HttpOnly + Secure + appropriate SameSite
    v
.NET BFF
    |
    | Validate / retrieve server-side session
    v
Session Store (e.g. Redis)
    |
    | Protected token material / token reference
    v
BFF obtains or retrieves access token
    |
    | Authorization: Bearer <access_token>
    v
APIM / Downstream APIs
```

**Advantages** - OAuth access and refresh tokens are not exposed to
browser JavaScript. - Centralized token renewal and downstream API
calls. - BFF session can be invalidated on logout. - Browser application
can use a conventional secure cookie session. - Can simplify
browser-side authentication and token-management code.

**Trade-offs** - Adds a BFF service and operational responsibility. -
Requires secure session/token storage and distributed-session design
where multiple BFF instances exist. - Requires CSRF protection and
secure cookie configuration. - BFF becomes an important security and
availability boundary. - Downstream access tokens may remain valid until
expiry unless separately revoked or rejected.

**Assessment:** Strong default for many enterprise browser applications,
especially when the frontend only needs to call its own backend.

------------------------------------------------------------------------

## 5. Compare the four browser approaches

  -----------------------------------------------------------------------
  Approach          Credential        JavaScript can    Main
                    location          read credential?  consideration
  ----------------- ----------------- ----------------- -----------------
  Local storage     Browser           Yes               XSS can steal
                    persistent                          token
                    storage                             

  In-memory         Browser           Yes               Lost on refresh;
                    JavaScript memory                   still exposed to
                                                        XSS

  Token in cookie   Browser cookie    Not if `HttpOnly` CSRF and token
                                      is correctly set  lifecycle

  BFF + opaque      Cookie in         No access to      Additional
  cookie            browser; OAuth    cookie value;     backend
                    tokens            tokens stay       infrastructure
                    server-side       server-side       
  -----------------------------------------------------------------------

This comparison is about common implementations. Actual security depends
on cookie scope, browser origin, token handling, CSRF controls, XSS
prevention and the overall deployment.

------------------------------------------------------------------------

## 6. BFF architecture for Angular + .NET 8 + Azure APIM

### 6.1 Logical architecture

``` text
+---------------------------------------------------+
|                    Browser                        |
|                                                   |
|                 Angular SPA                       |
|                                                   |
|       Opaque HttpOnly / Secure session cookie     |
+-------------------------+-------------------------+
                          |
                          | HTTPS request
                          v
+---------------------------------------------------+
|                    .NET 8 BFF                     |
|                                                   |
|  - Browser-facing session                         |
|  - Authentication / OIDC callback                 |
|  - CSRF protection                                |
|  - Session and token lifecycle                    |
|  - Downstream API orchestration                    |
+-------------------------+-------------------------+
                          |
             +------------+------------+
             |                         |
             v                         v
+------------------------+  +------------------------+
| Session / Token Store  |  | Identity Provider      |
|                        |  |                        |
| Redis / distributed DB |  | OAuth 2.0 / OIDC       |
| Protected token data   |  | Authorization Server   |
+------------------------+  +------------------------+
             |
             | BFF retrieves/uses protected token
             v
+---------------------------------------------------+
|              Azure API Management                |
|                                                   |
| - JWT validation (where appropriate)              |
| - API routing and policies                        |
| - Rate limiting and other gateway controls        |
+-------------------------+-------------------------+
                          |
                          v
+---------------------------------------------------+
|                 Backend APIs                      |
|                                                   |
|       Microservices / Domain Services              |
|                                                   |
| Validate access token and enforce authorization   |
+---------------------------------------------------+
```

### 6.2 Responsibility boundaries

  -----------------------------------------------------------------------
  Component                           Responsibility
  ----------------------------------- -----------------------------------
  Angular SPA                         User interface; calls BFF using the
                                      browser session

  .NET BFF                            Manages browser session, OAuth flow
                                      and downstream token use

  Identity Provider                   Authenticates user and issues
                                      tokens

  Redis / database                    Stores session state and, if
                                      needed, protected token material

  Azure Key Vault                     Stores application secrets,
                                      certificates and cryptographic keys

  Azure APIM                          API gateway, routing, policies and
                                      configured token validation

  Microservices                       Enforce their own authorization
                                      rules and validate tokens as
                                      required
  -----------------------------------------------------------------------

**Key Vault clarification:** Do not automatically use Key Vault as the
per-user access/refresh-token database. Key Vault is intended for
secrets, keys and certificates. A protected session/token store is a
different responsibility. Protect stored tokens with appropriate
encryption, access controls, retention and monitoring.

### 6.3 Typical browser request

1.  The user signs in through the BFF's supported OAuth/OIDC flow.
2.  The BFF establishes a server-side session.
3.  The BFF sends an opaque session cookie to the browser.
4.  Angular calls the BFF using that session.
5.  The BFF validates the session.
6.  The BFF obtains or retrieves the appropriate access token.
7.  The BFF calls APIM or a downstream API with the bearer token.
8.  The API validates the token and enforces authorization.
9.  The BFF returns the response to Angular.

**Important:** A true BFF pattern means browser API calls go through the
BFF. If Angular directly calls downstream APIs with access tokens, the
architecture is no longer using the BFF as the exclusive browser-facing
API boundary.

------------------------------------------------------------------------

## 7. Security threats and controls

  -----------------------------------------------------------------------
  Threat                  What happens            Important controls
  ----------------------- ----------------------- -----------------------
  XSS                     Malicious script runs   Output encoding, safe
                          in the application's    DOM APIs, CSP,
                          origin                  dependency controls,
                                                  secure development

  CSRF                    Browser is tricked into Anti-forgery token,
                          sending an              Origin validation,
                          authenticated request   SameSite, appropriate
                                                  request design

  Session hijacking       Attacker obtains or     HTTPS, Secure cookies,
                          reuses a valid session  session rotation,
                          credential              short/inactive expiry,
                                                  revocation

  Network sniffing        Traffic is observed on  TLS/HTTPS
                          the network             

  Man-in-the-middle       Communication is        TLS certificate
                          intercepted or altered  validation, secure
                                                  transport configuration

  Token replay            A stolen bearer token   Short token lifetime,
                          is reused               secure storage,
                                                  audience/scope
                                                  restrictions,
                                                  sender-constrained
                                                  tokens where
                                                  appropriate
  -----------------------------------------------------------------------

### 7.1 Important distinctions

-   `HttpOnly` helps prevent JavaScript from reading a cookie; it does
    not prevent XSS.
-   BFF reduces browser exposure of OAuth tokens; it does not stop
    malicious scripts from making requests through the user's
    authenticated browser.
-   `SameSite` helps reduce CSRF; it is not a complete substitute for
    CSRF defenses.
-   TLS protects data in transit; `Secure` ensures the browser sends the
    cookie only over HTTPS.
-   Logout that deletes a browser cookie does not necessarily revoke an
    already-issued access token.

### 7.2 CSRF protection in a BFF

For state-changing requests, consider:

-   Anti-forgery tokens or an appropriate signed double-submit
    implementation.
-   Origin/Referer validation where appropriate.
-   Correct `SameSite` cookie configuration.
-   Restricting accepted content types and request methods as
    appropriate.
-   Avoiding state-changing GET endpoints.

OAuth/OIDC sign-in flows also require their own protections, including
state and nonce validation and PKCE where applicable.

------------------------------------------------------------------------

## 8. Logout, expiry, refresh and revocation

These concepts should be designed together.

### 8.1 Logout in a BFF

A robust logout sequence generally includes:

1.  Receive a logout request from the browser.
2.  Validate the request against CSRF protections.
3.  Invalidate the server-side BFF session.
4.  Expire the browser session cookie.
5.  Revoke refresh tokens or the identity-provider session when
    supported and required by the logout design.
6.  Consider downstream access-token validity until expiry.

### 8.2 Why JWT revocation is different

Example:

-   10:00 --- User receives an access token valid for 30 minutes.
-   10:05 --- User logs out.
-   10:06 --- The copied access token may still pass offline signature
    and expiry validation.

Deleting the cookie does not invalidate that token.

Possible controls include:

-   Short-lived access tokens.
-   Refresh-token rotation and revocation.
-   Token denylist or server-side session checks.
-   Introspection or other online validity checks where supported.
-   Appropriate account/session revocation mechanisms.

The correct approach depends on the identity provider, token format and
risk requirements.

------------------------------------------------------------------------

## 9. Common misconceptions --- quick revision

  -----------------------------------------------------------------------
  Misconception                       Correct understanding
  ----------------------------------- -----------------------------------
  Cookies and JWT are alternatives    Cookie is a transport/storage
                                      mechanism; JWT is a token format

  All cookies require server-side     A cookie can carry an opaque
  sessions                            session ID, JWT or another value

  JWT means no server-side storage    Token validation can be stateless,
                                      but refresh and revocation may
                                      require state

  JWT is encrypted                    A normal signed JWT is readable;
                                      signing is not encryption

  JWT automatically solves scaling    Shared sessions can scale; JWT is
                                      one architectural option

  HttpOnly prevents XSS               It prevents JavaScript from reading
                                      the cookie, not script execution

  SameSite completely prevents CSRF   It reduces risk; additional
                                      controls may be necessary

  BFF eliminates all browser attacks  It reduces token exposure but does
                                      not eliminate XSS or CSRF

  Deleting a JWT cookie revokes the   It removes the browser's copy; an
  JWT                                 independently copied token may
                                      remain valid

  Key Vault should store every user's Use an appropriate protected
  token                               session/token store; Key Vault
                                      manages application secrets and
                                      keys
  -----------------------------------------------------------------------

------------------------------------------------------------------------

## 10. Architecture decision guide

  -----------------------------------------------------------------------
  Scenario                            Suitable starting point
  ----------------------------------- -----------------------------------
  Traditional server-rendered web     Secure cookie + server-side session
  application                         

  SPA calling APIs directly with      Carefully designed in-memory token
  OAuth tokens                        approach, with an appropriate
                                      OAuth/OIDC flow

  Enterprise SPA with sensitive APIs  BFF + opaque session cookie
  and centralized backend             

  Machine-to-machine integration      OAuth client credentials or another
                                      appropriate workload identity flow

  Distributed services validating     Signed access tokens with strict
  access tokens                       issuer, audience, signature and
                                      authorization validation

  Application requiring immediate     Server-side session or BFF session
  session termination                 with centralized invalidation
  -----------------------------------------------------------------------

No pattern is universally best. Consider threat model, browser/client
type, API topology, availability, operational maturity and revocation
requirements.

------------------------------------------------------------------------

## 11. .NET 8 implementation topics to explore

When implementing this architecture in .NET 8, investigate:

-   ASP.NET Core cookie authentication.
-   ASP.NET Core session middleware and distributed caching.
-   OpenID Connect Authorization Code Flow with PKCE.
-   OAuth confidential-client token handling at the BFF.
-   Secure cookie configuration: `HttpOnly`, `SecurePolicy`, `SameSite`,
    expiry and path.
-   Anti-forgery services and Origin validation.
-   Data Protection key persistence and sharing across BFF instances.
-   Redis-backed distributed session storage.
-   Protected storage of per-user OAuth tokens.
-   Access-token renewal and refresh-token rotation.
-   Logout and identity-provider session termination.
-   APIM JWT validation and downstream authorization.
-   Secret and certificate management with Azure Key Vault.

------------------------------------------------------------------------

## 12. Further reading

Use these as primary references and verify the current versions when
implementing:

1.  [OWASP Session Management Cheat
    Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html)
2.  [OWASP JSON Web Token Cheat
    Sheet](https://cheatsheetseries.owasp.org/cheatsheets/JSON_Web_Token_for_Java_Cheat_Sheet.html)
3.  [OWASP Cross-Site Request Forgery Prevention Cheat
    Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Cross-Site_Request_Forgery_Prevention_Cheat_Sheet.html)
4.  [IETF OAuth 2.0 for Browser-Based
    Applications](https://datatracker.ietf.org/doc/draft-ietf-oauth-browser-based-apps/)
5.  [Microsoft --- ASP.NET Core
    authentication](https://learn.microsoft.com/aspnet/core/security/authentication/)
6.  [Microsoft --- ASP.NET Core Data
    Protection](https://learn.microsoft.com/aspnet/core/security/data-protection/introduction)
7.  [Microsoft --- Azure API Management validate-jwt
    policy](https://learn.microsoft.com/azure/api-management/validate-jwt-policy)

------------------------------------------------------------------------

## 13. Final takeaway

Remember these five statements:

1.  **Cookie is a mechanism; session is state; JWT is a token format.**
2.  **JWT can enable stateless API validation, but does not eliminate
    all server-side state.**
3.  **Browser storage choices must account for both XSS and CSRF.**
4.  **A BFF keeps OAuth tokens server-side and exposes an application
    session to the browser.**
5.  **Secure authentication is a complete lifecycle: sign-in, token
    validation, authorization, renewal, logout, revocation and session
    protection.**

**Recommended default for an enterprise Angular + .NET browser
application:** Start by evaluating a BFF with an opaque `HttpOnly`,
`Secure` session cookie, appropriate `SameSite` configuration, CSRF
protection, distributed session management where needed, and server-side
OAuth token handling.
