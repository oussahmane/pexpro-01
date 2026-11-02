export const customerStatementPrintCss = `
@media print {
  body * {
    visibility: hidden !important;
  }

  [data-testid="customer-statement"],
  [data-testid="customer-statement"] * {
    visibility: visible !important;
  }

  [data-testid="customer-statement"] {
    position: absolute;
    left: 0;
    top: 0;
    width: 100%;
  }
}
`;
