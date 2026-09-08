# Online account deletion — implementation and deployment

September 8, 2026. Client and backend source implemented; 93 Unity tests and 10
Node tests pass. The revised Cloud Code script has **not** been deployed, and
end-to-end deletion from an installed game is not yet verified.

## Player flow

New players use local simulated trading. Online Data explains the connection;
Connect Online opts into the anonymous account. Existing online accounts keep
their connection. Online Data is reachable from title, pause, and market screens,
including when no character remains.

Delete Account opens a separate warning with Keep Account and Delete Forever.
The warning explains that listed artifacts, unclaimed proceeds, and the online
balance are removed. Local characters, carried items, coins, and the cosmetic
purchase remain. No password, payment details, or private token is shown.

## Ordering and failure behavior

1. Serialize complete market operations. Do not begin deletion while a trade or
   connection is still updating its local state.
2. Persist `marketDeletionPending` before contacting a service. Block further
   market changes and new online connections while it remains true.
3. Resume only the saved Authentication session with `CreateAccount = false`.
   Match its player ID against the recorded account. Never create a replacement
   account to process a deletion request.
4. Invoke `deleteAccountData`. Under the existing Cloud Save write lock, remove
   that player's listings, balance/proceeds, ownership references, and cached
   mutation responses. Preserve assets already bought by other players.
5. Require the explicit `accountDataDeleted` response flag, then persist that
   checkpoint before calling Unity Authentication's `DeleteAccountAsync`.
6. Clear the pending request and online identity only after confirmed success.
   Return to local trading. A retry repeats the idempotent market cleanup and
   refreshes its replay barrier before attempting Authentication deletion again.

Market cleanup retains a private player-ID/expiry entry for replay protection.
It blocks old connect/list/coin requests for two hours. Expired entries are pruned
on a subsequent successful market write; on an idle backend they can remain
stored longer. This is not a scheduled hard-deletion deadline. Define and verify
the required production cleanup schedule and disclose actual retention before
publishing the privacy policy. Unity documents one-hour access-token validity in
its [session-management guide](https://docs.unity.com/en-us/authentication/session-management).

## Evidence

- Client tests cover persistence before network work, restart/retry, positive
  market confirmation before account removal, missing credentials, mismatched
  account IDs, concurrent operations, and preservation of local progress and
  purchased cosmetics. Fresh local trading makes no gateway initialization call.
- Backend tests cover removal of targeted records, preserving unrelated buyers,
  deletion/purchase write-lock races, idempotent retry, stale request rejection,
  and expired-barrier cleanup.
- `AuthenticationDeletionApiVerification.json` records a live test against one
  newly created disposable Authentication account. Initial deletion returned 200;
  repeated deletion returned 404 `RESOURCE_NOT_FOUND`; fetching the removed user
  returned 404 with `user not found`; restoring its session returned 401
  `INVALID_SESSION_TOKEN`. The test account was removed, temporary credentials
  deleted, and no existing player or market data touched.
- QA screenshots use the explicit `qa-preview-only` fixture. The first delete
  tap is asserted to show confirmation only. They do not prove backend deletion.

## Remaining release gates

1. Deploy `Assets/CloudCode/ArtifactMarket.js` to a dedicated test environment
   for the configured Unity project. The new response flag is deliberately
   required: an older or incomplete server must not trigger Authentication deletion.
2. Create disposable test sellers/buyers there. Exercise purchase before deletion,
   deletion before purchase, retries, disconnection between phases, and restart
   of the game. Inspect `DUNGEON_DASH_MARKET/state` and Authentication to verify
   exactly the intended account and records were removed.
3. Establish the missing-confirmation support route. If Authentication removes
   an account but its reply is lost, a restarted client may have only invalid
   credentials. Invalid/missing credentials alone do not prove account deletion.
   The current client keeps the request unconfirmed and provides a support ID.
   A working contact and authorized operator verification/recovery process are
   still required; this case is not claimed as automatically solved.
4. Review retention of service logs and replay barriers. Do not equate
   Authentication deletion with deletion from every other service. Unity's
   [account-deletion documentation](https://docs.unity.com/en-us/authentication/delete-accounts)
   explicitly separates those responsibilities.
5. Verify the screens, interruption behavior, and API flow on a physical iPhone,
   then update the hosted policy and App Privacy disclosures before submission.

No production deployment, existing-account deletion, or public policy update
has been performed by this implementation.
