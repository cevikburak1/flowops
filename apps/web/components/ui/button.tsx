import { cva, type VariantProps } from "class-variance-authority";
import { clsx } from "clsx";
import { Slot } from "radix-ui";
import type { ComponentProps } from "react";
import { twMerge } from "tailwind-merge";

const buttonVariants = cva("button", {
  variants: {
    variant: { primary: "primary", secondary: "secondary", danger: "danger", icon: "icon-button" },
  },
  defaultVariants: { variant: "primary" },
});

type ButtonProps = ComponentProps<"button"> & VariantProps<typeof buttonVariants> & { asChild?: boolean };

export function Button({ className, variant, asChild = false, ...props }: ButtonProps) {
  const Component = asChild ? Slot.Root : "button";
  return <Component className={twMerge(clsx(buttonVariants({ variant }), className))} {...props} />;
}
