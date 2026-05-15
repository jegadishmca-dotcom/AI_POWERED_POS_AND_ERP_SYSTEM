import React from 'react';
import { CartItem } from '../types';

export const CartView = ({ items }: { items: CartItem[] }) => {
  return (
    <div className="flex-1 bg-white overflow-y-auto">
      <table className="min-w-full divide-y divide-gray-200">
        <thead className="bg-gray-50 sticky top-0">
          <tr>
            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Item</th>
            <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">Qty</th>
            <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">Price</th>
            <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">Total</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-200">
          {items.map((item, idx) => (
            <tr key={idx} className="hover:bg-blue-50">
              <td className="px-6 py-4 text-sm font-medium text-gray-900">{item.name}</td>
              <td className="px-6 py-4 text-sm text-gray-500 text-right">{item.quantity}</td>
              <td className="px-6 py-4 text-sm text-gray-500 text-right">{item.unitPrice.toFixed(2)}</td>
              <td className="px-6 py-4 text-sm text-gray-900 font-bold text-right">{item.totalAmount.toFixed(2)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
};
