"""
Copyright Epic Games, Inc. All Rights Reserved.

Generate lorelib wrappers based on Jinja2 template.
"""

import os

from jinja2 import Environment, FileSystemLoader

from common import util


def render_content(
    jinja_env, template_file, content_filedir, content_filename, symbols
):
    """Renders a jinja2 template to generate the appropriate JS file"""
    template = jinja_env.get_template(template_file)
    content = template.render(symbols=symbols)
    os.makedirs(content_filedir, exist_ok=True)
    with open(content_filename, "w", encoding="utf-8") as f:
        f.write(content)


def generate_templates(
    header_file, templates_dir, generate_targets, ast_visitor, build_augmented
):
    """Generates the given set of templates

    Args:
        templates_dir: path to .ji templates
        generate_targets: an array of (template.ji, target_dir, target_file_name) tuples
        ast_visitor: custom visitor for the AST interpretation
    """

    print("Loading and cleaning lore header", end=" ")
    ast, line_comments = util.load_and_clean_header(header_file)
    print("done.")

    print("Parsing lore header", end=" ")
    lore_visitor = ast_visitor(line_comments)
    lore_visitor.visit(ast)
    print("done.")

    augmented = build_augmented(lore_visitor) if build_augmented else {}

    jinja_env = Environment(
        loader=FileSystemLoader(templates_dir), trim_blocks=True, lstrip_blocks=True
    )
    jinja_env.globals["pascal_case"] = util.pascal_case
    jinja_env.globals["camel_case"] = util.camel_case
    for key, value in augmented.items():
        jinja_env.globals[key] = value

    for js_template, directory, file_name in generate_targets:
        render_content(
            jinja_env,
            js_template,
            directory,
            os.path.join(directory, file_name),
            lore_visitor,
        )
