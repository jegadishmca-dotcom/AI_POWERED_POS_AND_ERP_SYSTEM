export const formatCurrency = (value: number | undefined | null): string => {
  if (value === undefined || value === null || Number.isNaN(value)) return '₹0.00';
  return new Intl.NumberFormat('en-IN', {
    style: 'currency',
    currency: 'INR',
  }).format(value);
};
