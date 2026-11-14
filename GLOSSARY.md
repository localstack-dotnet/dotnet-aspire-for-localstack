# LocalStack-backed development and testing

Language for the runtime choices offered by this integration.

## Language

**LocalStack runtime**:
An instance of a selected LocalStack container image, with the capabilities enabled by its configuration and entitlements.
_Avoid_: Hobby integration, Community integration, Pro integration

**Legacy runtime**:
A token-free LocalStack container version retained as an explicitly selected compatibility option. The term describes a runtime choice, not a separate integration edition, and does not identify a particular image version or imply feature equivalence with current runtimes.
_Avoid_: Free runtime, Hobby runtime

**Authenticated runtime**:
A LocalStack runtime that requires a platform authentication token to start, with available capabilities determined by its associated plan.
_Avoid_: Paid runtime, Pro runtime
