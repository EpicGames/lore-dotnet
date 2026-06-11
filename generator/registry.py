"""
Copyright Epic Games, Inc. All Rights Reserved.

Type registries used by the Jinja templates. The seed maps below are the
hand-curated mappings for primitive, scalar, and event-data-array types.
`build_augmented` extends them with auto-detected entries for every
`*_array_t` typedef and every enum found in `lore.h`, so that adding a new
array type or enum to the header requires zero edits here or in `utils.ji`.
"""

from common import util


# Defaults for C# field initialization. The auto-loops in `build_augmented`
# register defaults for everything they can derive (enums, *_array_t types,
# auto-generated wrapper structs), so manual entries here are only needed
# for primitives, hand-written scalar wrappers, hand-written hardcoded
# blit classes, and event-data types that live in `visitor.events`.
SEED_INIT_MAP = {
    "nuint": "0",
    "int[]": "Array.Empty<int>()",
    "uint[]": "Array.Empty<uint>()",
    "string": "string.Empty",
    "LoreContext": "new()",
    "IntPtr": "IntPtr.Zero",
    "byte[]": "Array.Empty<byte>()",
    "byte[][]": "Array.Empty<byte[]>()",
    "LoreRevisionSyncProgressEventData": "new()",
    "LoreRepositoryVerifyFragmentMatchEventData[]": "Array.Empty<LoreRepositoryVerifyFragmentMatchEventData>()",
}


# C-type → C#-type mappings the auto-loops in `build_augmented` can't
# derive. Everything that *can* be derived is registered later via
# `setdefault`, so adding an entry here is only required for:
#   - C primitives                            (uint8_t, int32_t, void*, ...)
#   - Hand-written scalar wrappers exposed as primitives or byte[]
#                                              (lore_string_t→string,
#                                              lore_hash_t→byte[], ...)
#   - Pointer-to-element forms                 (lore_string_t*, ...)
#   - Hand-written struct wrappers in
#     `SEED_HARDCODED_BLIT_CLASSES`            (lore_metadata_t, ...)
#   - Event-data types that live in
#     `visitor.events` (not `visitor.types`)
SEED_CS_MAP = {
    "void*": "IntPtr",
    "uint8_t": "byte",
    "uint16_t": "short",
    "int": "int",
    "int32_t": "int",
    "uint32_t": "uint",
    "int64_t": "long",
    "uint64_t": "ulong",
    "uintptr_t": "nuint",
    "uint32_t*": "IntPtr",
    "lore_branch_point_t*": "IntPtr",
    "lore_metadata_type_t*": "IntPtr",
    "lore_string_t": "string",
    "lore_string_t*": "IntPtr",
    "lore_binary_t": "byte[]",
    "lore_hash_t": "byte[]",
    "lore_hash_t[]": "byte[][]",
    "lore_hash_array_t": "byte[][]",
    "lore_context_t": "byte[]",
    "lore_partition_t": "byte[]",
    "lore_branch_id_t": "byte[]",
    "lore_repository_id_t": "byte[]",
    "lore_metadata_t": "LoreMetadata",
    "lore_revision_sync_progress_event_data_t": "_LoreRevisionSyncProgressEventData",
    "lore_instance_id_t": "byte[]",
    "lore_repository_verify_fragment_match_event_data_array_t": "LoreRepositoryVerifyFragmentMatchEventData[]",
    "lore_bytes_t": "byte[]",
    "lore_store_t": "LoreStore",
    "lore_node_id_t": "uint",
}


SEED_BLIT_TYPES = [
    "lore_binary_t",
    "lore_string_t",
    "lore_context_t",
    "lore_partition_t",
    "lore_branch_id_t",
    "lore_repository_id_t",
    "lore_hash_t",
    "lore_hash_array_t",
    "lore_metadata_t",
    "lore_address_t",
    "lore_fragment_t",
    "lore_branch_point_t",
    "lore_log_config_t",
    "lore_instance_id_t",
    "lore_bytes_t",
]


SEED_HARDCODED_BLIT_CLASSES = [
    "lore_hash_t",
    "lore_event_t",
    "lore_binary_t",
    "lore_string_t",
    "lore_context_t",
    "lore_partition_t",
    "lore_branch_id_t",
    "lore_repository_id_t",
    "lore_metadata_t",
    "lore_event_callback_config_t",
    "lore_repository_verify_fragment_match_event_data_array_t",
    "lore_instance_id_t",
    "lore_store_t",
    "lore_bytes_t",
]


UNCOMMON_FUNCTIONS = [
    "lore_event_type",
    "lore_log_configure",
    "lore_shutdown",
    "lore_set_allocator",
    "lore_version",
    "lore_user_directory",
    "lore_set_thread_limit",
]


# Element-c-type → (wrapper_class, native_cs_type, to_native_per_element_call).
# `to_native_per_element_call` is the C# expression used to convert one
# element from the wrapper struct back to the native type — see types.ji.
NATIVE_WRAPPER_ELEMENTS = {
    "lore_string_t": ("LoreString", "string", "LoreString.ToNative({element})"),
    "lore_instance_id_t": ("LoreInstanceId", "byte[]", "{element}.Data"),
}


PRIMITIVE_INT_ELEMENTS = {
    "uint16_t": "short",
    "uint32_t": "uint",
    "uint64_t": "ulong",
    "int32_t": "int",
    "uintptr_t": "nuint",
}


def _annotation_for_element(element_c_type, enums):
    """Compute (category, cs_annotation, element_class, element_cs_type) for
    an array element. element_class is the C# type used inside the native
    buffer (a Span<element_class>); element_cs_type is the public-facing
    type exposed in the C# constructor signature.
    """
    if element_c_type == "uint8_t":
        return ("primitive_bool", "bool[]", "byte", "bool")
    if element_c_type in PRIMITIVE_INT_ELEMENTS:
        cs_type = PRIMITIVE_INT_ELEMENTS[element_c_type]
        return ("primitive_int", f"{cs_type}[]", cs_type, cs_type)
    if element_c_type in enums:
        enum_class = util.pascal_case(element_c_type.removesuffix("_t"))
        return ("enum", f"{enum_class}[]", enum_class, enum_class)
    if element_c_type in NATIVE_WRAPPER_ELEMENTS:
        wrapper_class, native_type, _ = NATIVE_WRAPPER_ELEMENTS[element_c_type]
        return ("native_wrapper", f"{native_type}[]", wrapper_class, native_type)
    element_class = util.pascal_case(element_c_type.removesuffix("_t"))
    return ("struct", f"{element_class}[]", element_class, element_class)


def detect_from_ffi_struct_types(types_dict, hardcoded_blit_classes):
    """Auto-generated wrapper struct types from `types.ji`.

    Any entry in `visitor.types` that isn't an `*_array_t` and isn't
    hand-written (i.e. not in `hardcoded_blit_classes`) is emitted by
    `types.ji` as a `[StructLayout(LayoutKind.Sequential)]` C# struct
    with a `Clone()` method. Returning the full set means every field
    referencing one resolves cleanly in cs_map without manual registry
    edits, so adding a new nested struct to `lore.h` is zero-edit here.
    """
    detected = set()
    for struct_name in types_dict:
        if struct_name.endswith("_array_t"):
            continue
        if struct_name in hardcoded_blit_classes:
            continue
        detected.add(struct_name)
    return sorted(detected)


def detect_array_types(types_dict, enums_dict):
    """Find every `*_array_t` in `types_dict` and return a list of dicts."""
    detected = []
    for struct_name, fields in types_dict.items():
        if not struct_name.endswith("_array_t"):
            continue
        if len(fields) != 2:
            continue
        ptr_field_type, ptr_field_name, _, _ = fields[0]
        _, count_field_name, _, _ = fields[1]
        if not ptr_field_type.endswith("*"):
            continue
        element_c_type = ptr_field_type[:-1]
        category, cs_annotation, element_class, element_cs_type = _annotation_for_element(
            element_c_type, enums_dict
        )
        to_native_call = None
        if category == "native_wrapper":
            to_native_call = NATIVE_WRAPPER_ELEMENTS[element_c_type][2]
        # Split element_cs_type for jagged-array allocation syntax:
        # "byte[]" → base "byte", rank "[]" so the template emits
        # `new byte[N][]` instead of the invalid `new byte[][N]`.
        bracket_idx = element_cs_type.find("[")
        if bracket_idx == -1:
            element_cs_type_base = element_cs_type
            element_cs_type_rank = ""
        else:
            element_cs_type_base = element_cs_type[:bracket_idx]
            element_cs_type_rank = element_cs_type[bracket_idx:]
        # Marshal.SizeOf<EnumType>() throws at runtime ("cannot be marshaled
        # as an unmanaged structure"). C enums always marshal as uint here,
        # so substitute uint for the size-of call on enum elements.
        marshal_size_type = "uint" if category == "enum" else element_class
        detected.append(
            {
                "array_c_type": struct_name,
                "array_class": util.pascal_case(struct_name.removesuffix("_t")),
                "element_c_type": element_c_type,
                "element_class": element_class,
                "element_cs_type": element_cs_type,
                "element_cs_type_base": element_cs_type_base,
                "element_cs_type_rank": element_cs_type_rank,
                "marshal_size_type": marshal_size_type,
                "ptr_field": ptr_field_name,
                "count_field": count_field_name,
                "cs_annotation": cs_annotation,
                "category": category,
                "to_native_call": to_native_call,
            }
        )
    return detected


def build_augmented(visitor):
    """Return augmented copies of the seed registries plus the array_types list."""
    array_types = detect_array_types(visitor.types, visitor.enums)
    # Event-data-array typedefs (ending in `event_data_array_t`) live in
    # `visitor.events`. They get a different code shape (FFI-only Clone()
    # wrapper class) emitted by a dedicated loop in events_types.ji.
    event_array_types = detect_array_types(visitor.events, visitor.enums)

    cs_map = dict(SEED_CS_MAP)
    init_map = dict(SEED_INIT_MAP)
    blit_types = list(SEED_BLIT_TYPES)
    hardcoded_blit_classes = list(SEED_HARDCODED_BLIT_CLASSES)

    for enum_c_type, values in visitor.enums.items():
        enum_class = util.pascal_case(enum_c_type.removesuffix("_t"))
        cs_map.setdefault(enum_c_type, enum_class)
        if values:
            init_map.setdefault(enum_class, f"{enum_class}.{values[0]}")

    for arr in array_types:
        cs_map[arr["array_c_type"]] = arr["cs_annotation"]
        init_map.setdefault(arr["cs_annotation"], f"Array.Empty<{arr['element_cs_type']}>()")
        if arr["array_c_type"] not in blit_types:
            blit_types.append(arr["array_c_type"])
        if arr["array_c_type"] not in hardcoded_blit_classes:
            hardcoded_blit_classes.append(arr["array_c_type"])

    # Register event-data-array types so the main events_types.ji loop
    # skips them (auto-add to hardcoded_blit_classes); the dedicated loop
    # at the bottom of events_types.ji emits the FFI/Clone wrappers.
    for arr in event_array_types:
        cs_map.setdefault(arr["array_c_type"], arr["cs_annotation"])
        init_map.setdefault(arr["cs_annotation"], f"Array.Empty<{arr['element_cs_type']}>()")
        if arr["array_c_type"] not in hardcoded_blit_classes:
            hardcoded_blit_classes.append(arr["array_c_type"])

    # Auto-detect every type generated by types.ji and register it in
    # cs_map / init_map so any field referencing it (e.g. a nested
    # struct in an *_args_t) resolves cleanly. Manual seed entries
    # override these via setdefault.
    from_ffi_struct_types = detect_from_ffi_struct_types(
        visitor.types, hardcoded_blit_classes
    )
    for sw in from_ffi_struct_types:
        wrapper_class = util.pascal_case(sw.removesuffix("_t"))
        cs_map.setdefault(sw, wrapper_class)
        init_map.setdefault(wrapper_class, "new()")

    return {
        "cs_map": cs_map,
        "init_map": init_map,
        "blit_types": blit_types,
        "hardcoded_blit_classes": hardcoded_blit_classes,
        "from_ffi_struct_types": from_ffi_struct_types,
        "uncommon_functions": UNCOMMON_FUNCTIONS,
        "array_types": array_types,
        "event_array_types": event_array_types,
    }
