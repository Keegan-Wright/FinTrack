# Users and admins

Admins see two extra pages, **Users** and **Automation**. This covers the Users page.

## Passwords

Passwords need at least 15 characters, and any characters will do. A few words together is easier to remember than
a short jumble. Passwords made before this rule keep working until they're next changed.

## Reset links

If someone has forgotten their password and can't get a reset email, make them a reset link and send it yourself. It
works once, until it expires. They choose the new password, you never see it.

## Two-factor sign in

Users with two-factor sign in on show a **Two-factor** chip. For someone who's lost their phone and their recovery
codes, **Turn off two-factor** turns it off.

- Their authenticator key and recovery codes are replaced, so the old ones never work again.
- They're signed out everywhere, and sign in next with just their password.
- They're emailed to say an admin turned it off.
- It's logged with who did it.

You can't turn off your own here. Use **Your profile**, which asks for your password.

## Passkeys

Users with passkeys show how many they have. For someone who's lost the devices they're on, **Remove passkeys**
removes them all. Make sure it's really them asking first.

- None of their passkeys can sign in any more.
- They sign in with their password, and can add new passkeys from **Your profile**.
- They're emailed to say an admin removed them.
- It's logged with who did it.

You can't remove your own here. Use **Your profile**, which asks for your password.

## API tokens

Users with API tokens that still work show how many they have. **Revoke tokens** revokes them all, for example if one
has leaked.

- They stop working straight away.
- The user is emailed to say an admin revoked them, and can make new ones.
- It's logged with who did it.

You can't see anyone's tokens or make tokens for them. To revoke your own, use **API tokens**.

## Locking

Locking someone signs them out within a minute and stops them signing in until you unlock them. Their API tokens stop
working straight away too. Their banks aren't synced and they get no notifications meanwhile.

Admins can't be locked. Remove their admin access first.

## Admin access

**Add admin access** and **Remove admin access** change who's an admin. The change reaches them within a minute.

- The last admin's access can't be removed while password sign in is on.
- Removing your own access takes you to the dashboard.
- For people who sign in through single sign-on, your provider's `admins` group decides instead, at their next sign
  in. See [Single sign-on](../self-hosting/single-sign-on.md#admins).
